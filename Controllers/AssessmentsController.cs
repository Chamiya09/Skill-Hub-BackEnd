using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Assessments;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public sealed class AssessmentsController : ControllerBase
    {
        private readonly IAssessmentService _assessmentService;
        private readonly ILogger<AssessmentsController> _logger;

        public AssessmentsController(
            IAssessmentService assessmentService,
            ILogger<AssessmentsController> logger)
        {
            _assessmentService = assessmentService;
            _logger = logger;
        }

        #region 1. HR Assessment Creation & HITL Question Generation

        /// <summary>
        /// Option 1: HR manually creates an Assessment with custom coding questions.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateAssessmentManual(
            [FromBody] CreateAssessmentManualDto dto,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.CreateAssessmentManualAsync(dto, hrManagerId, cancellationToken);
            return CreatedAtAction(nameof(GetAssessmentById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Option 2: AI drafts coding questions based on Job Title, JD, and skills (Human-in-the-Loop).
        /// </summary>
        [HttpPost("generate-questions")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GenerateQuestionsWithAi(
            [FromBody] GenerateAiQuestionsRequestDto dto,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.GenerateQuestionsWithAiAsync(dto, hrManagerId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// HR updates draft questions, passing threshold, or time limit.
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateAssessment(
            Guid id,
            [FromBody] UpdateAssessmentDto dto,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.UpdateAssessmentAsync(id, dto, hrManagerId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// HR approves and publishes an assessment template.
        /// </summary>
        [HttpPost("{id:guid}/publish")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> PublishAssessment(
            Guid id,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.PublishAssessmentAsync(id, hrManagerId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// HR archives an outdated assessment template.
        /// </summary>
        [HttpPost("{id:guid}/archive")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> ArchiveAssessment(
            Guid id,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.ArchiveAssessmentAsync(id, hrManagerId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// List all assessments for a specific job vacancy (HR view).
        /// </summary>
        [HttpGet("job/{jobVacancyId:guid}")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(IReadOnlyList<AssessmentResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssessmentsByJob(
            Guid jobVacancyId,
            CancellationToken cancellationToken)
        {
            var result = await _assessmentService.GetAssessmentsByJobAsync(jobVacancyId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Retrieve published tracks for dropdowns (matches modal 'Select Assessment Track').
        /// </summary>
        [HttpGet("job/{jobVacancyId:guid}/tracks")]
        [ProducesResponseType(typeof(IReadOnlyList<AssessmentTrackSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssessmentTracksByJob(
            Guid jobVacancyId,
            CancellationToken cancellationToken)
        {
            var result = await _assessmentService.GetAssessmentTracksByJobAsync(jobVacancyId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Get a single assessment by ID with complete question bank.
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssessmentById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _assessmentService.GetAssessmentByIdAsync(id, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Delete an outdated assessment template.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteAssessment(
            Guid id,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var deleted = await _assessmentService.DeleteAssessmentAsync(id, hrManagerId, cancellationToken);
            if (!deleted) return NotFound(new { message = "Assessment not found." });
            return NoContent();
        }

        #endregion

        #region 2. HR Dispatch Assessment (Modal Action)

        /// <summary>
        /// Dispatches a skill assessment to a shortlisted candidate.
        /// Implements incoming contract from Student 2 & creates candidate submission.
        /// </summary>
        [HttpPost("dispatch")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(DispatchAssessmentResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> DispatchAssessment(
            [FromBody] DispatchAssessmentRequestDto dto,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.DispatchAssessmentAsync(dto, hrManagerId, cancellationToken);
            return Ok(result);
        }

        #endregion

        #region 3. Candidate Examination Endpoints

        /// <summary>
        /// Candidate fetches sanitized question paper (solutions & hidden test cases stripped).
        /// </summary>
        [HttpGet("take/{submissionId:guid}")]
        [ProducesResponseType(typeof(StartExamResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetExamPaper(
            Guid submissionId,
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            var paper = await _assessmentService.GetExamPaperAsync(submissionId, candidateId, cancellationToken);
            return Ok(paper);
        }

        /// <summary>
        /// Candidate clicks 'Start Exam'; marks startedAt and initiates timer.
        /// </summary>
        [HttpPost("take/{submissionId:guid}/start")]
        [ProducesResponseType(typeof(StartExamResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> StartExam(
            Guid submissionId,
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            var paper = await _assessmentService.StartExamAsync(submissionId, candidateId, cancellationToken);
            return Ok(paper);
        }

        /// <summary>
        /// Proctoring telemetry: records tab switches, window blur events, and full-screen exits.
        /// </summary>
        [HttpPost("take/{submissionId:guid}/proctor-event")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> LogProctorEvent(
            Guid submissionId,
            [FromBody] ProctorEventRequestDto eventDto,
            CancellationToken cancellationToken)
        {
            var logged = await _assessmentService.LogProctorEventAsync(submissionId, eventDto, cancellationToken);
            if (!logged) return NotFound(new { message = "Submission not found." });
            return Ok(new { success = true });
        }

        /// <summary>
        /// Candidate submits code solutions. Triggers auto-grading and evaluates passing threshold.
        /// </summary>
        [HttpPost("take/{submissionId:guid}/submit")]
        [ProducesResponseType(typeof(SubmissionDetailDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> SubmitExam(
            Guid submissionId,
            [FromBody] SubmitAnswersRequestDto answersDto,
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            var result = await _assessmentService.SubmitAnswersAsync(submissionId, answersDto, candidateId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Get submission details and score breakdown.
        /// </summary>
        [HttpGet("submissions/{submissionId:guid}")]
        [ProducesResponseType(typeof(SubmissionDetailDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSubmissionDetail(
            Guid submissionId,
            CancellationToken cancellationToken)
        {
            var result = await _assessmentService.GetSubmissionDetailAsync(submissionId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Delete rejected candidate's submission data (compliance / data pruning).
        /// </summary>
        [HttpDelete("submissions/{submissionId:guid}")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteSubmission(
            Guid submissionId,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var deleted = await _assessmentService.DeleteSubmissionAsync(submissionId, hrManagerId, cancellationToken);
            if (!deleted) return NotFound(new { message = "Submission not found." });
            return NoContent();
        }

        #endregion

        #region 4. Leaderboard & Student 3 Outgoing Contract

        /// <summary>
        /// HR reads the ranked candidate leaderboard for a job requisition.
        /// </summary>
        [HttpGet("job/{jobVacancyId:guid}/leaderboard")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(IReadOnlyList<LeaderboardEntryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLeaderboard(
            Guid jobVacancyId,
            CancellationToken cancellationToken)
        {
            var leaderboard = await _assessmentService.GetLeaderboardAsync(jobVacancyId, cancellationToken);
            return Ok(leaderboard);
        }

        /// <summary>
        /// Promotes the Top 5 candidates to 'Assessment Passed', marks others 'Rejected',
        /// and emits the outgoing contract payload for Student 3 (Meeting Orchestration).
        /// </summary>
        [HttpPost("job/{jobVacancyId:guid}/finalize-top5")]
        [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
        [ProducesResponseType(typeof(FinalizeTop5ResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> FinalizeTop5(
            Guid jobVacancyId,
            CancellationToken cancellationToken)
        {
            var hrManagerId = GetCurrentUserId();
            var result = await _assessmentService.FinalizeTop5Async(jobVacancyId, hrManagerId, cancellationToken);
            return Ok(result);
        }

        #endregion

        #region Private Helpers

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("companyId")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId");

            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        private Guid? GetCandidateId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId");

            return Guid.TryParse(claim, out var id) ? id : null;
        }

        #endregion
    }
}

