using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.CvEvaluation;
using Skill_Hub_BackEnd.Services.Interfaces;
using System.Security.Claims;

namespace Skill_Hub_BackEnd.Controllers
{
    /// <summary>
    /// Handles the AI-powered Candidate CV Evaluation feature.
    ///
    /// Endpoints:
    ///   POST /api/CVEvaluation/analyze         → Runs the three-agent pipeline
    ///   POST /api/CVEvaluation/{id}/approve     → Human-in-the-loop approval gate
    ///   GET  /api/CVEvaluation/{id}             → Fetch evaluation by ID
    ///   GET  /api/CVEvaluation/latest           → Fetch latest evaluation for candidate/job pair
    ///
    /// All endpoints require a valid JWT Bearer token for a Company role.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // All endpoints require authentication — company recruiter JWT
    public sealed class CVEvaluationController : ControllerBase
    {
        private readonly IAgenticCvService _agenticCvService;
        private readonly ILogger<CVEvaluationController> _logger;

        public CVEvaluationController(
            IAgenticCvService agenticCvService,
            ILogger<CVEvaluationController> logger)
        {
            _agenticCvService = agenticCvService;
            _logger           = logger;
        }

        // ────────────────────────────────────────────────────────────────────────
        // POST /api/CVEvaluation/analyze
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Triggers the three-agent CV evaluation pipeline:
        ///   Agent 1 (Extractor) → Agent 2 (Evaluator) → Agent 3 (Validator)
        ///
        /// Called by the React frontend's "Run AI CV Evaluation" button onClick handler.
        /// </summary>
        /// <remarks>
        /// The response binds directly to the Candidate Evaluation Dashboard UI:
        /// - <c>matchScore</c>  → Score ring / gauge widget
        /// - <c>strengths</c>   → Green strength chip list
        /// - <c>missingSkills</c> → Red gap chip list
        /// - <c>recommendation</c> → AI recommendation banner
        /// </remarks>
        [HttpPost("analyze")]
        [ProducesResponseType(typeof(CvEvaluationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Analyze(
            [FromBody] AnalyzeCvRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var companyId = GetCurrentCompanyId();
            if (companyId == Guid.Empty)
                return Unauthorized(new { message = "Company identity could not be resolved from token." });

            _logger.LogInformation(
                "[CVEvaluationController] POST /analyze — CandidateId={CandidateId}, JobId={JobId}",
                request.CandidateId, request.JobId);

            try
            {
                var result = await _agenticCvService.AnalyzeCvAsync(request, companyId, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "[CVEvaluationController] Resource not found during analysis.");
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CVEvaluationController] Unexpected error during CV analysis.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "An unexpected error occurred while running the CV evaluation pipeline.",
                    detail  = ex.Message,
                });
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // POST /api/CVEvaluation/{id}/approve
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finalises the Human-in-the-Loop (HITL) approval gate.
        ///
        /// Called by the React frontend's "Approve &amp; Shortlist" button onClick handler
        /// after the recruiter reviews the AI evaluation report.
        /// </summary>
        [HttpPost("{id:guid}/approve")]
        [ProducesResponseType(typeof(ApproveEvaluationResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Approve(
            [FromRoute] Guid id,
            [FromBody] ApproveEvaluationRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var approvingUserId = GetCurrentUserId();
            if (approvingUserId == Guid.Empty)
                return Unauthorized(new { message = "User identity could not be resolved from token." });

            _logger.LogInformation(
                "[CVEvaluationController] POST /{Id}/approve — Decision={Decision}, UserId={UserId}",
                id, request.Decision, approvingUserId);

            try
            {
                var result = await _agenticCvService.ApproveEvaluationAsync(id, request, approvingUserId, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CVEvaluationController] Error during approval of evaluation {Id}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // GET /api/CVEvaluation/{id}
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a single evaluation result by its unique ID.
        /// The frontend can call this to re-hydrate a previously run evaluation on page load.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(CvEvaluationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _agenticCvService.GetEvaluationByIdAsync(id, cancellationToken);
            return result is null
                ? NotFound(new { message = $"Evaluation {id} not found." })
                : Ok(result);
        }

        // ────────────────────────────────────────────────────────────────────────
        // GET /api/CVEvaluation/latest?candidateId=...&jobId=...
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the most recent evaluation result for a candidate/job pair.
        /// Use this to check if an evaluation already exists before triggering a new run.
        /// </summary>
        [HttpGet("latest")]
        [ProducesResponseType(typeof(CvEvaluationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLatest(
            [FromQuery] Guid candidateId,
            [FromQuery] Guid jobId,
            CancellationToken cancellationToken)
        {
            var result = await _agenticCvService.GetLatestEvaluationAsync(candidateId, jobId, cancellationToken);
            return result is null
                ? NotFound(new { message = "No evaluation found for this candidate/job pair." })
                : Ok(result);
        }

        // ────────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS: JWT Claim Extraction
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts the authenticated user's company ID from JWT claims.
        /// Expects the claim key "companyId" injected by TokenService during login.
        /// </summary>
        private Guid GetCurrentCompanyId()
        {
            var raw = User.FindFirstValue("companyId");
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Extracts the authenticated user's own User ID from the standard NameIdentifier claim.
        /// </summary>
        private Guid GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}
