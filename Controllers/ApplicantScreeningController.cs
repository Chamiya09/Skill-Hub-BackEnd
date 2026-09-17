using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/jobs/{jobId:guid}")]
    [Authorize(Roles = "Company,Employer,Admin,HR_Admin,Recruiter,Hiring_Manager")]
    [Produces("application/json")]
    public sealed class ApplicantScreeningController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IApplicantScreeningService _screeningService;

        public ApplicantScreeningController(
            ApplicationDbContext dbContext,
            IApplicantScreeningService screeningService)
        {
            _dbContext = dbContext;
            _screeningService = screeningService;
        }

        [HttpPost("run-ai-screen")]
        [ProducesResponseType(typeof(IReadOnlyList<ScreenedApplicantDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> RunAiScreen(
            Guid jobId,
            [FromQuery] bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            var companyId = GetCurrentCompanyId();
            if (companyId is null)
                return Unauthorized(new { message = "A company identifier is required." });

            try
            {
                return Ok(await _screeningService.FetchAndRankApplicantsAsync(
                    jobId, companyId.Value, forceRefresh, cancellationToken));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "AI screening is temporarily unavailable." });
            }
        }

        [HttpGet("applicants")]
        [ProducesResponseType(typeof(IReadOnlyList<ScreenedApplicantDto>), StatusCodes.Status200OK)]
        public Task<IActionResult> GetRankedApplicants(Guid jobId, CancellationToken cancellationToken) =>
            GetRankedApplicantsCore(jobId, cancellationToken);

        private async Task<IActionResult> GetRankedApplicantsCore(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            var companyId = GetCurrentCompanyId();
            if (companyId is null)
                return Unauthorized(new { message = "A company identifier is required." });

            try
            {
                return Ok(await _screeningService.FetchAndRankApplicantsAsync(
                    jobId, companyId.Value, forceRefresh: false, cancellationToken));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (Exception exception) when (
                exception is HttpRequestException or JsonException or TaskCanceledException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "AI screening is temporarily unavailable." });
            }
        }

        [HttpPost("move-to-shortlist")]
        public async Task<IActionResult> MoveToShortlist(
            Guid jobId,
            [FromBody] List<Guid> candidateIds,
            CancellationToken cancellationToken)
        {
            if (candidateIds.Count == 0) return BadRequest(new { message = "Select at least one candidate." });
            var companyId = GetCurrentCompanyId();
            if (companyId is null) return Unauthorized(new { message = "A company identifier is required." });
            if (!await _dbContext.JobVacancies.AsNoTracking().AnyAsync(
                job => job.Id == jobId && job.CompanyId == companyId.Value,
                cancellationToken))
                return NotFound(new { message = "Job not found or access denied." });

            var distinctIds = candidateIds.Distinct().ToList();
            var applications = await _dbContext.JobApplications
                .Where(application => application.JobId == jobId &&
                    distinctIds.Contains(application.CandidateId) && application.Status == "Applied")
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var application in applications)
            {
                application.Status = "Shortlisted";
                application.UpdatedAt = now;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = $"{applications.Count} candidate(s) moved to Shortlisted.",
                updatedCount = applications.Count,
            });
        }

        [HttpPost("remove-from-shortlist")]
        public async Task<IActionResult> RemoveFromShortlist(
            Guid jobId,
            [FromBody] List<Guid> candidateIds,
            CancellationToken cancellationToken)
        {
            if (candidateIds.Count == 0) return BadRequest(new { message = "Select at least one candidate." });
            var companyId = GetCurrentCompanyId();
            if (companyId is null) return Unauthorized(new { message = "A company identifier is required." });
            if (!await _dbContext.JobVacancies.AsNoTracking().AnyAsync(
                job => job.Id == jobId && job.CompanyId == companyId.Value,
                cancellationToken))
                return NotFound(new { message = "Job not found or access denied." });

            var distinctIds = candidateIds.Distinct().ToList();
            var applications = await _dbContext.JobApplications
                .Where(application => application.JobId == jobId &&
                    distinctIds.Contains(application.CandidateId) && application.Status == "Shortlisted")
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var application in applications)
            {
                application.Status = "Applied";
                application.UpdatedAt = now;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = $"{applications.Count} candidate(s) removed from Shortlist.",
                updatedCount = applications.Count,
            });
        }

        /// <summary>
        /// Returns all shortlisted applicants for a job, with full profile data for the Hiring Pipeline board.
        /// Endpoint: GET /api/jobs/{jobId}/shortlisted
        /// </summary>
        [HttpGet("shortlisted")]
        [ProducesResponseType(typeof(IReadOnlyList<ShortlistedApplicantDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetShortlisted(Guid jobId, CancellationToken cancellationToken)
        {
            var companyId = GetCurrentCompanyId();
            if (companyId is null)
                return Unauthorized(new { message = "A company identifier is required." });

            if (!await _dbContext.JobVacancies.AsNoTracking().AnyAsync(
                job => job.Id == jobId && job.CompanyId == companyId.Value, cancellationToken))
                return NotFound(new { message = "Job not found or access denied." });

            var applications = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(a => a.JobId == jobId && a.Status == "Shortlisted")
                .Include(a => a.Candidate)
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync(cancellationToken);

            var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();

            var aiScores = await _dbContext.AiMatchResults
                .AsNoTracking()
                .Where(r => r.JobId == jobId && candidateIds.Contains(r.CandidateId))
                .ToDictionaryAsync(r => r.CandidateId, r => r.MatchPercentage, cancellationToken);

            var skillsMap = await _dbContext.CandidateSkills
                .AsNoTracking()
                .Where(s => candidateIds.Contains(s.UserId))
                .GroupBy(s => s.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SkillName).ToList(), cancellationToken);

            var experienceMap = await _dbContext.CandidateExperiences
                .AsNoTracking()
                .Where(e => candidateIds.Contains(e.UserId))
                .GroupBy(e => e.UserId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.OrderByDescending(e => e.IsCurrent).FirstOrDefault(),
                    cancellationToken);

            var result = applications.Select(app =>
            {
                var c = app.Candidate;
                experienceMap.TryGetValue(app.CandidateId, out var topExp);

                var headline = c?.Headline;
                if (string.IsNullOrWhiteSpace(headline) && topExp != null)
                    headline = string.IsNullOrWhiteSpace(topExp.Company)
                        ? topExp.Title
                        : $"{topExp.Title} at {topExp.Company}";
                headline ??= "Candidate Profile";

                return new ShortlistedApplicantDto
                {
                    ApplicationId = app.Id,
                    CandidateId = app.CandidateId,
                    FullName = !string.IsNullOrWhiteSpace(c?.FullName) ? c!.FullName
                             : !string.IsNullOrWhiteSpace(c?.Email) ? c.Email
                             : "Candidate",
                    Email = c?.Email ?? string.Empty,
                    Phone = c?.Phone,
                    Headline = headline,
                    Location = c?.Location ?? topExp?.Location ?? "Location unspecified",
                    AvatarUrl = c?.AvatarUrl,
                    Skills = skillsMap.TryGetValue(app.CandidateId, out var skills) ? skills : new(),
                    AppliedDate = app.AppliedDate,
                    ShortlistedAt = app.UpdatedAt,
                    AiMatchScore = aiScores.TryGetValue(app.CandidateId, out var score) ? score : null,
                };
            }).ToList();

            return Ok(result);
        }

        private Guid? GetCurrentCompanyId()
        {
            var companyClaim = User.FindFirst("companyId")?.Value
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            return Guid.TryParse(companyClaim, out var companyId) ? companyId : null;
        }
    }
}
