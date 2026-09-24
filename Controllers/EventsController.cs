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
    [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventsController(IEventService eventService)
        {
            _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
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
        /// Retrieve events for the Monthly Planner view, optionally filtered by month/year or date range.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<EventResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEvents(
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
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
                cancellationToken);

            return Ok(events);
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

