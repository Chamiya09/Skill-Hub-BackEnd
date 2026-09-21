using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.InterviewPrep;
using Skill_Hub_BackEnd.Services.Implementations;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InterviewPrepController : ControllerBase
    {
        private readonly IInterviewPrepService _interviewPrepService;
        private readonly ILogger<InterviewPrepController> _logger;

        public InterviewPrepController(
            IInterviewPrepService interviewPrepService,
            ILogger<InterviewPrepController> logger)
        {
            _interviewPrepService = interviewPrepService;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/interviewprep/generate
        /// Generates an AI-tailored interview preparation guide.
        /// Accepts Application ID, checks candidate eligibility, saves to PostgreSQL, and returns newly created GuideId.
        /// Returns 403 Forbidden if candidate's application is Rejected.
        /// </summary>
        [HttpPost("generate")]
        public async Task<ActionResult<GenerateInterviewPrepResponseDto>> Generate(
            [FromBody] GenerateInterviewPrepRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!request.ApplicationId.HasValue && string.IsNullOrWhiteSpace(request.JobDescription) && string.IsNullOrWhiteSpace(request.TargetRole) && string.IsNullOrWhiteSpace(request.JobTitle))
            {
                return BadRequest(new { message = "Application ID, Job description, or Target Role is required to generate interview preparation guidelines." });
            }

            var candidateId = GetCandidateId() ?? request.CandidateId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

            try
            {
                var result = await _interviewPrepService.GenerateGuideAsync(
                    candidateId,
                    request,
                    cancellationToken);

                return Ok(new GenerateInterviewPrepResponseDto
                {
                    GuideId = result.Id,
                    Id = result.Id,
                    Message = "Interview preparation guide generated successfully.",
                    Guide = result
                });
            }
            catch (InterviewPrepIneligibleException ex)
            {
                _logger.LogWarning("Access denied for Candidate {CandidateId}: {Message} (Status: {Status})", candidateId, ex.Message, ex.Status);
                return StatusCode(ex.StatusCode, new { message = ex.Message, status = ex.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating interview prep guide for Candidate {CandidateId}", candidateId);
                return StatusCode(500, new { message = "An error occurred while generating your interview preparation guide. Please try again." });
            }
        }

        /// <summary>
        /// GET /api/interviewprep/{id}
        /// Fetches the saved guide from the database and returns it to the frontend for Page 2 (Study Dashboard).
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<InterviewPrepGuideDto>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var guide = await _interviewPrepService.GetGuideByIdAsync(id, cancellationToken);
            if (guide == null)
            {
                return NotFound(new { message = "Interview preparation guide not found." });
            }

            return Ok(guide);
        }

        /// <summary>
        /// GET /api/interviewprep/eligibility
        /// Checks if the candidate is eligible to access interview preparation.
        /// </summary>
        [HttpGet("eligibility")]
        public async Task<ActionResult<InterviewPrepEligibilityDto>> CheckEligibility(
            [FromQuery] Guid? applicationId,
            [FromQuery] Guid? jobId,
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId() ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

            var result = await _interviewPrepService.CheckEligibilityAsync(
                candidateId,
                applicationId,
                jobId,
                cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// GET /api/interviewprep/latest
        /// Retrieves the most recent preparation guide for the logged-in candidate.
        /// </summary>
        [HttpGet("latest")]
        public async Task<ActionResult<InterviewPrepGuideDto>> GetLatest(
            [FromQuery] Guid? jobId,
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            if (candidateId == null)
            {
                return Unauthorized(new { message = "Authentication is required to view your saved interview prep guide." });
            }

            var guide = await _interviewPrepService.GetLatestGuideAsync(
                candidateId.Value,
                jobId,
                cancellationToken);

            if (guide == null)
            {
                return NotFound(new { message = "No interview preparation guide found." });
            }

            return Ok(guide);
        }

        private Guid? GetCandidateId()
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("id");

            return Guid.TryParse(subject, out var candidateId) ? candidateId : null;
        }
    }
}
