using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Ai;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/candidate")]
    [Authorize]
    [Produces("application/json")]
    public class JobMatchController : ControllerBase
    {
        private readonly IAiAgentService _aiAgentService;
        private readonly ILogger<JobMatchController> _logger;

        public JobMatchController(
            IAiAgentService aiAgentService,
            ILogger<JobMatchController> logger)
        {
            _aiAgentService = aiAgentService;
            _logger = logger;
        }

        [HttpPost("analyze-job")]
        [ProducesResponseType(typeof(AiMatchResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AnalyzeJob([FromBody] AiMatchRequestDto request)
        {
            try
            {
                return Ok(await _aiAgentService.AnalyzeCandidateMatchAsync(request));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "LangGraph job-match analysis failed.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "Unable to complete the AI match analysis at this time." });
            }
        }
    }
}
