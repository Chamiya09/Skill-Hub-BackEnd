using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.InterviewPrep;
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
        /// </summary>
        [HttpPost("generate")]
        public async Task<ActionResult<InterviewPrepGuideDto>> Generate(
            [FromBody] GenerateInterviewPrepRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(request.JobDescription))
            {
                return BadRequest(new { message = "Job description is required to generate interview preparation guidelines." });
            }

            var candidateId = GetCandidateId() ?? Guid.Parse("11111111-1111-1111-1111-111111111111");

            try
            {
                var result = await _interviewPrepService.GenerateGuideAsync(
                    candidateId,
                    request,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating interview prep guide for Candidate {CandidateId}", candidateId);
                return StatusCode(500, new { message = "An error occurred while generating your interview preparation guide. Please try again." });
            }
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
