using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Dashboard;
using Skill_Hub_BackEnd.DTOs.Jobs;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext dbContext, ILogger<DashboardController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves aggregated ATS dashboard statistics and metrics for the authenticated company.
        /// Endpoint: GET /api/dashboard/stats
        /// </summary>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDashboardStats()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(companyIdClaim, out var companyId))
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in token." });
            }

            var company = await _dbContext.Companies.FindAsync(companyId);
            var companyName = company?.CompanyName ?? string.Empty;

            var companyJobs = await _dbContext.JobVacancies
                .Where(j => j.CompanyId == companyId)
                .ToListAsync();

            var activeCount = companyJobs.Count(j => string.Equals(j.Status, "Active", StringComparison.OrdinalIgnoreCase));
            var draftCount = companyJobs.Count(j => string.Equals(j.Status, "Draft", StringComparison.OrdinalIgnoreCase));
            var closedCount = companyJobs.Count(j => string.Equals(j.Status, "Closed", StringComparison.OrdinalIgnoreCase));
            var totalCount = companyJobs.Count;
            var companyJobIds = companyJobs.Select(job => job.Id).ToList();

            var applications = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(application => companyJobIds.Contains(application.JobId))
                .ToListAsync();

            var aiResults = await _dbContext.AiMatchResults
                .AsNoTracking()
                .Where(result => companyJobIds.Contains(result.JobId))
                .ToListAsync();

            var weekStart = DateTime.UtcNow.Date.AddDays(-6);
            var totalCandidates = applications.Select(application => application.CandidateId).Distinct().Count();
            var candidatesThisWeek = applications
                .Where(application => application.AppliedDate >= weekStart)
                .Select(application => application.CandidateId)
                .Distinct()
                .Count();
            var shortlistedCount = applications.Count(application =>
                application.Status.Contains("Shortlist", StringComparison.OrdinalIgnoreCase));
            var pendingInterviews = applications.Count(application =>
                application.Status.Contains("Interview", StringComparison.OrdinalIgnoreCase));
            var aiShortlisted = aiResults.Count(result => result.MatchPercentage >= 80);
            var evaluatedPairs = aiResults
                .Select(result => (result.CandidateId, result.JobId))
                .ToHashSet();
            var pendingAiEvaluations = applications.Count(application =>
                !evaluatedPairs.Contains((application.CandidateId, application.JobId)));

            var topTalentMatches = await _dbContext.AiMatchResults
                .AsNoTracking()
                .Where(result => companyJobIds.Contains(result.JobId))
                .OrderByDescending(result => result.MatchPercentage)
                .ThenByDescending(result => result.CreatedAt)
                .Take(4)
                .Select(result => new TopTalentMatchDto
                {
                    CandidateId = result.CandidateId,
                    CandidateName = result.Candidate != null ? result.Candidate.FullName : "Candidate",
                    Headline = result.Candidate != null ? result.Candidate.Headline : null,
                    JobId = result.JobId,
                    JobTitle = result.Job != null ? result.Job.Title : "Vacancy",
                    MatchPercentage = result.MatchPercentage,
                    EvaluatedAt = result.CreatedAt,
                })
                .ToListAsync();

            var vacancyMetrics = companyJobs
                .Where(job => string.Equals(job.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(job => job.CreatedAt)
                .Take(5)
                .Select(job => new OverviewVacancyDto
                {
                    JobId = job.Id,
                    Title = job.Title,
                    Department = job.Department,
                    Status = job.Status,
                    ApplicantsCount = applications.Count(application => application.JobId == job.Id),
                    AiScreenedCount = aiResults.Count(result => result.JobId == job.Id),
                })
                .ToList();

            var latestAiResult = aiResults.OrderByDescending(result => result.CreatedAt).FirstOrDefault();
            var latestAiJob = latestAiResult == null
                ? null
                : companyJobs.FirstOrDefault(job => job.Id == latestAiResult.JobId);

            var departmentsCount = companyJobs
                .Select(j => j.Department.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var recentJobs = companyJobs
                .OrderByDescending(j => j.CreatedAt)
                .Take(5)
                .Select(j => new JobResponseDto
                {
                    Id = j.Id,
                    CompanyId = j.CompanyId,
                    CompanyName = companyName,
                    Title = j.Title,
                    Department = j.Department,
                    Location = j.Location,
                    EmploymentType = j.EmploymentType,
                    ExperienceLevel = j.ExperienceLevel,
                    SalaryRange = j.SalaryRange,
                    Status = j.Status,
                    Description = j.Description,
                    WhatWeOffer = j.WhatWeOffer,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt
                })
                .ToList();

            var statsDto = new DashboardStatsDto
            {
                ActiveVacanciesCount = activeCount,
                DraftVacanciesCount = draftCount,
                ClosedVacanciesCount = closedCount,
                TotalVacanciesCount = totalCount,
                TotalDepartmentsCount = departmentsCount,
                TotalCandidatesCount = totalCandidates,
                CandidatesThisWeekCount = candidatesThisWeek,
                AiScreenedCount = aiResults.Count,
                AiShortlistedCount = aiShortlisted,
                ShortlistedCount = shortlistedCount,
                PendingInterviewsCount = pendingInterviews,
                PendingAiEvaluationsCount = pendingAiEvaluations,
                TopTalentMatches = topTalentMatches,
                VacancyMetrics = vacancyMetrics,
                RecentAiActivity = latestAiResult == null ? null : new RecentAiActivityDto
                {
                    JobId = latestAiResult.JobId,
                    JobTitle = latestAiJob?.Title ?? "Vacancy",
                    MatchPercentage = latestAiResult.MatchPercentage,
                    OccurredAt = latestAiResult.CreatedAt,
                },
                RecentVacancies = recentJobs
            };

            return Ok(statsDto);
        }
    }
}
