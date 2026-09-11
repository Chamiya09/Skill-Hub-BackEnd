using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Jobs;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/public/jobs")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class PublicJobsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<PublicJobsController> _logger;

        public PublicJobsController(ApplicationDbContext dbContext, ILogger<PublicJobsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all public active job vacancies across all companies.
        /// Endpoint: GET /api/public/jobs
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<JobResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPublicJobs(
            [FromQuery] string? search = null,
            [FromQuery] string? department = null,
            [FromQuery] string? employmentType = null,
            [FromQuery] string? experienceLevel = null,
            [FromQuery] int? limit = null)
        {
            var query = _dbContext.JobVacancies
                .Include(j => j.Company)
                .Where(j => j.Status == "Active")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(j =>
                    j.Title.ToLower().Contains(term) ||
                    j.Department.ToLower().Contains(term) ||
                    j.Location.ToLower().Contains(term) ||
                    (j.Company != null && j.Company.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(department) && !string.Equals(department, "All", StringComparison.OrdinalIgnoreCase) && !string.Equals(department, "All Roles", StringComparison.OrdinalIgnoreCase))
            {
                var deptTerm = department.Trim().ToLower();
                query = query.Where(j => j.Department.ToLower().Contains(deptTerm));
            }

            if (!string.IsNullOrWhiteSpace(employmentType) && !string.Equals(employmentType, "All", StringComparison.OrdinalIgnoreCase))
            {
                var empTypeTerm = employmentType.Trim().ToLower();
                query = query.Where(j => j.EmploymentType.ToLower() == empTypeTerm);
            }

            if (!string.IsNullOrWhiteSpace(experienceLevel) && !string.Equals(experienceLevel, "All", StringComparison.OrdinalIgnoreCase))
            {
                var expTerm = experienceLevel.Trim().ToLower();
                query = query.Where(j => j.ExperienceLevel.ToLower().Contains(expTerm));
            }

            query = query.OrderByDescending(j => j.CreatedAt);

            if (limit.HasValue && limit.Value > 0)
            {
                query = query.Take(limit.Value);
            }

            var jobs = await query.ToListAsync();

            var response = jobs.Select(j => new JobResponseDto
            {
                Id = j.Id,
                CompanyId = j.CompanyId,
                CompanyName = j.Company?.CompanyName ?? "Enterprise Employer",
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
            });

            return Ok(response);
        }

        /// <summary>
        /// Retrieves the public details of a single active job vacancy by its ID.
        /// Endpoint: GET /api/public/jobs/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPublicJobById(Guid id)
        {
            var job = await _dbContext.JobVacancies
                .Include(j => j.Company)
                .FirstOrDefaultAsync(j => j.Id == id && j.Status == "Active");

            if (job == null)
            {
                return NotFound(new { message = $"Active job vacancy with ID '{id}' was not found." });
            }

            var response = new JobResponseDto
            {
                Id = job.Id,
                CompanyId = job.CompanyId,
                CompanyName = job.Company?.CompanyName ?? "Enterprise Employer",
                Title = job.Title,
                Department = job.Department,
                Location = job.Location,
                EmploymentType = job.EmploymentType,
                ExperienceLevel = job.ExperienceLevel,
                SalaryRange = job.SalaryRange,
                Status = job.Status,
                Description = job.Description,
                WhatWeOffer = job.WhatWeOffer,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            };

            return Ok(response);
        }
    }
}
