using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Events;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager,Candidate,CANDIDATE,User")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly IHolidayService _holidayService;
        private readonly IInterviewSchedulerService _schedulerService;

        public EventsController(
            IEventService eventService,
            IHolidayService holidayService,
            IInterviewSchedulerService schedulerService)
        {
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            _holidayService = holidayService ?? throw new ArgumentNullException(nameof(holidayService));
            _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        }

        /// <summary>
        /// Retrieves national and public holidays for the Monthly Planner calendar (Google Calendar API synced with server-side caching).
        /// Endpoint: GET /api/Events/holidays?year=2026&month=9&country=LK
        /// </summary>
        [HttpGet("holidays")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IReadOnlyList<NationalHolidayDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHolidays(
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] string? country,
            CancellationToken cancellationToken)
        {
            var targetYear = year.HasValue && year.Value > 2000 ? year.Value : DateTime.UtcNow.Year;
            var holidays = await _holidayService.GetHolidaysAsync(targetYear, month, country ?? "LK", cancellationToken);
            return Ok(holidays);
        }

        /// <summary>
        /// HR creates an event for the Monthly Planner calendar.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEvent(
            [FromBody] CreateEventDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var result = await _eventService.CreateEventAsync(dto, hrManagerId, companyId, cancellationToken);
            return CreatedAtAction(nameof(GetEventById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Retrieve events for the Monthly Planner view, optionally filtered by department, month/year, or date range.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<EventResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEvents(
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? department,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var events = await _eventService.GetEventsAsync(
                companyId,
                hrManagerId,
                year,
                month,
                startDate,
                endDate,
                department,
                cancellationToken);

            return Ok(events);
        }

        /// <summary>
        /// Retrieves the list of departments that currently have at least one active job vacancy.
        /// </summary>
        [HttpGet("departments")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveDepartments(CancellationToken cancellationToken)
        {
            var companyId = GetCurrentCompanyId();
            var departments = await _eventService.GetActiveDepartmentsAsync(companyId, cancellationToken);
            return Ok(departments);
        }

        /// <summary>
        /// Retrieve a single event by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var ev = await _eventService.GetEventByIdAsync(id, hrManagerId, companyId, cancellationToken);
            if (ev == null)
            {
                return NotFound(new { message = $"Event with ID '{id}' was not found." });
            }

            return Ok(ev);
        }

        /// <summary>
        /// Update an existing event in the Monthly Planner.
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(
            Guid id,
            [FromBody] CreateEventDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var updated = await _eventService.UpdateEventAsync(id, dto, hrManagerId, companyId, cancellationToken);
            if (updated == null)
            {
                return NotFound(new { message = $"Event with ID '{id}' was not found or could not be updated." });
            }

            return Ok(updated);
        }

        /// <summary>
        /// Delete an event from the Monthly Planner.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEvent(
            Guid id,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var deleted = await _eventService.DeleteEventAsync(id, hrManagerId, companyId, cancellationToken);
            if (!deleted)
            {
                return NotFound(new { message = $"Event with ID '{id}' was not found or could not be deleted." });
            }

            return NoContent();
        }

        /// <summary>
        /// Internal endpoint consumed by Python AI Scheduler Agent to fetch interview candidates for a vacancy.
        /// </summary>
        [HttpGet("internal/interview-candidates")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(InterviewCandidatesResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInterviewCandidates(
            [FromQuery] Guid jobVacancyId,
            CancellationToken cancellationToken)
        {
            if (jobVacancyId == Guid.Empty)
                return BadRequest(new { message = "Valid jobVacancyId is required." });

            var result = await _schedulerService.GetCandidatesForInterviewAsync(jobVacancyId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Internal endpoint consumed by Python AI Scheduler Agent to fetch existing calendar events and blocked slots.
        /// </summary>
        [HttpGet("internal/existing-events")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(BlockedSlotsResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetExistingEvents(
            [FromQuery] Guid? companyId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var end = endDate ?? start.AddDays(30);

            var result = await _schedulerService.GetBlockedSlotsAsync(companyId, start, end, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Internal endpoint consumed by Python AI Scheduler Agent to fetch company schedule configuration.
        /// </summary>
        [HttpGet("internal/schedule-config")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ScheduleConfigDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetScheduleConfig(
            [FromQuery] Guid? companyId,
            CancellationToken cancellationToken)
        {
            var result = await _schedulerService.GetScheduleConfigAsync(companyId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// HR generates an AI interview schedule draft proposal. Does not persist to database.
        /// </summary>
        [HttpPost("generate-interview-schedule")]
        [ProducesResponseType(typeof(ScheduleProposalResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateInterviewSchedule(
            [FromBody] GenerateScheduleRequestDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            try
            {
                var proposal = await _schedulerService.GenerateScheduleProposalAsync(dto, hrManagerId, companyId, cancellationToken);
                return Ok(proposal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// HR confirms and saves approved interview schedule slots to database (Human-in-the-loop).
        /// </summary>
        [HttpPost("confirm-interview-schedule")]
        [ProducesResponseType(typeof(ConfirmInterviewScheduleResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmInterviewSchedule(
            [FromBody] ConfirmInterviewScheduleDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var result = await _schedulerService.ConfirmInterviewScheduleAsync(dto, hrManagerId, companyId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// HR manually schedules or reschedules an interview for a specific candidate.
        /// Endpoint: POST /api/Events/schedule-candidate-interview
        /// </summary>
        [HttpPost("schedule-candidate-interview")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ScheduleCandidateInterview(
            [FromBody] ScheduleCandidateInterviewDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            try
            {
                var result = await _eventService.ScheduleCandidateInterviewAsync(
                    dto, hrManagerId, companyId, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update meeting link / location for a scheduled interview event.
        /// Endpoint: PUT /api/Events/{id}/meeting-link
        /// </summary>
        [HttpPut("{id:guid}/meeting-link")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMeetingLink(
            Guid id,
            [FromBody] UpdateMeetingLinkDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var hrManagerId = GetCurrentUserId();
            var companyId = GetCurrentCompanyId();

            var updated = await _eventService.UpdateEventMeetingLinkAsync(id, dto.MeetingLink, hrManagerId, companyId, cancellationToken);
            if (updated == null)
            {
                return NotFound(new { message = $"Event with ID '{id}' was not found or could not be updated." });
            }

            return Ok(updated);
        }

        /// <summary>
        /// Retrieves upcoming and scheduled interviews for the authenticated candidate.
        /// Endpoint: GET /api/Events/my-interviews
        /// </summary>
        [HttpGet("my-interviews")]
        [Authorize(Roles = "Candidate,CANDIDATE,User")]
        [ProducesResponseType(typeof(IReadOnlyList<CandidateInterviewEventDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyInterviews(CancellationToken cancellationToken)
        {
            var candidateId = GetCurrentUserId();
            if (candidateId == Guid.Empty)
                return Unauthorized(new { message = "Candidate authentication required." });

            var interviews = await _schedulerService.GetCandidateInterviewsAsync(candidateId, cancellationToken);
            return Ok(interviews);
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("companyId")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId");

            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        private Guid? GetCurrentCompanyId()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            if (Guid.TryParse(companyIdClaim, out var cId)) return cId;

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, "Company", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Employer", StringComparison.OrdinalIgnoreCase))
            {
                return GetCurrentUserId();
            }

            return null;
        }
    }
}

