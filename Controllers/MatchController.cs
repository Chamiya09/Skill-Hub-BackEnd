using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Ai;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/match")]
    [Authorize(Roles = "Candidate,CANDIDATE")]
    [Produces("application/json")]
    public sealed class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly ILogger<MatchController> _logger;

        public MatchController(IMatchService matchService, ILogger<MatchController> logger)
        {
            _matchService = matchService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(AiMatchResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Get(
            [FromQuery] Guid candidateId,
            [FromQuery] Guid jobId,
            CancellationToken cancellationToken)
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var authenticatedId)) return Unauthorized();
            if (authenticatedId != candidateId) return Forbid();

            try
            {
                return Ok(await _matchService.AnalyzeAsync(candidateId, jobId, cancellationToken));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception,
                    "Match analysis failed for candidate {CandidateId} and job {JobId}.",
                    candidateId, jobId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "AI match analysis is temporarily unavailable." });
            }
        }
    }
}
