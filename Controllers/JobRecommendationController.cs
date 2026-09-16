using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Recommendations;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/candidate/{id:guid}/recommended-jobs")]
    [Authorize(Roles = "Candidate,CANDIDATE")]
    [Produces("application/json")]
    public sealed class JobRecommendationController : ControllerBase
    {
        private readonly IJobRecommendationService _recommendationService;
        private readonly ILogger<JobRecommendationController> _logger;

        public JobRecommendationController(
            IJobRecommendationService recommendationService,
            ILogger<JobRecommendationController> logger)
        {
            _recommendationService = recommendationService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<RecommendedJobResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetRecommendedJobs(
            Guid id,
            CancellationToken cancellationToken)
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(subject, out var authenticatedCandidateId))
            {
                return Unauthorized(new { message = "Invalid authentication token." });
            }

            if (authenticatedCandidateId != id)
            {
                return Forbid();
            }

            try
            {
                var recommendations = await _recommendationService
                    .GetRecommendedJobsAsync(id, cancellationToken);
                return Ok(recommendations);
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogError(exception, "AI job recommendations failed for candidate {CandidateId}.", id);
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { message = "Recommendations are temporarily unavailable." });
            }
        }
    }
}
