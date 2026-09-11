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
                RecentVacancies = recentJobs
            };

            return Ok(statsDto);
        }
    }
}
