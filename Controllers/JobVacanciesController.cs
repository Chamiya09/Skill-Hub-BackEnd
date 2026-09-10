using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/jobs")]
    [Authorize]
    [Produces("application/json")]
    public class JobVacanciesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<JobVacanciesController> _logger;

        public JobVacanciesController(ApplicationDbContext dbContext, ILogger<JobVacanciesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new job vacancy for the authenticated company.
        /// Endpoint: POST /api/jobs
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var companyId = GetCurrentCompanyId();
            if (companyId == null)
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in authentication claims." });
            }

            var company = await _dbContext.Companies.FindAsync(companyId.Value);
            if (company == null)
            {
                return Unauthorized(new { message = "Associated company account not found." });
            }

            var job = new JobVacancy
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId.Value,
                Title = dto.Title.Trim(),
                Department = dto.Department.Trim(),
                Location = dto.Location.Trim(),
                EmploymentType = dto.EmploymentType.Trim(),
                ExperienceLevel = dto.ExperienceLevel.Trim(),
                SalaryRange = dto.SalaryRange?.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status.Trim(),
                Description = dto.Description,
                WhatWeOffer = dto.WhatWeOffer,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.JobVacancies.Add(job);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyId} successfully created job vacancy {JobId} ('{Title}')", companyId.Value, job.Id, job.Title);

            var response = MapToResponseDto(job, company.CompanyName);
            return CreatedAtAction(nameof(GetJobById), new { id = job.Id }, response);
        }

        /// <summary>
        /// Retrieves all job vacancies for the authenticated company.
        /// Endpoint: GET /api/jobs
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<JobResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCompanyJobs()
        {
            var companyId = GetCurrentCompanyId();
            if (companyId == null)
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in authentication claims." });
            }

            var company = await _dbContext.Companies.FindAsync(companyId.Value);
            var companyName = company?.CompanyName ?? string.Empty;

            var jobs = await _dbContext.JobVacancies
                .Where(j => j.CompanyId == companyId.Value)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            var response = jobs.Select(j => MapToResponseDto(j, companyName));
            return Ok(response);
        }

        /// <summary>
        /// Retrieves the details of a specific job vacancy owned by the authenticated company.
        /// Endpoint: GET /api/jobs/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetJobById(Guid id)
        {
            var companyId = GetCurrentCompanyId();
            if (companyId == null)
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in authentication claims." });
            }

            var job = await _dbContext.JobVacancies
                .Include(j => j.Company)
                .FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == companyId.Value);

            if (job == null)
            {
                return NotFound(new { message = $"Job vacancy with ID '{id}' was not found or belongs to another company." });
            }

            var response = MapToResponseDto(job, job.Company?.CompanyName ?? string.Empty);
            return Ok(response);
        }

        /// <summary>
        /// Updates an existing job vacancy owned by the authenticated company.
        /// Endpoint: PUT /api/jobs/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateJob(Guid id, [FromBody] UpdateJobDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var companyId = GetCurrentCompanyId();
            if (companyId == null)
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in authentication claims." });
            }

            var job = await _dbContext.JobVacancies
                .Include(j => j.Company)
                .FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == companyId.Value);

            if (job == null)
            {
                return NotFound(new { message = $"Job vacancy with ID '{id}' was not found or belongs to another company." });
            }

            job.Title = dto.Title.Trim();
            job.Department = dto.Department.Trim();
            job.Location = dto.Location.Trim();
            job.EmploymentType = dto.EmploymentType.Trim();
            job.ExperienceLevel = dto.ExperienceLevel.Trim();
            job.SalaryRange = dto.SalaryRange?.Trim();
            job.Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status.Trim();
            job.Description = dto.Description;
            job.WhatWeOffer = dto.WhatWeOffer;
            job.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyId} successfully updated job vacancy {JobId} ('{Title}')", companyId.Value, job.Id, job.Title);

            var response = MapToResponseDto(job, job.Company?.CompanyName ?? string.Empty);
            return Ok(response);
        }

        /// <summary>
        /// Hard-deletes a job vacancy directly from the database.
        /// Endpoint: DELETE /api/jobs/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteJob(Guid id)
        {
            var companyId = GetCurrentCompanyId();
            if (companyId == null)
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in authentication claims." });
            }

            var job = await _dbContext.JobVacancies
                .FirstOrDefaultAsync(j => j.Id == id && j.CompanyId == companyId.Value);

            if (job == null)
            {
                return NotFound(new { message = $"Job vacancy with ID '{id}' was not found or belongs to another company." });
            }

            // CRUCIAL: Direct hard-delete from the database
            _dbContext.JobVacancies.Remove(job);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyId} permanently hard-deleted job vacancy {JobId}", companyId.Value, id);

            return NoContent();
        }

        /// <summary>
        /// Helper to extract the CompanyId from the authenticated user's JWT claims.
        /// </summary>
        private Guid? GetCurrentCompanyId()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(companyIdClaim, out var companyId))
            {
                return companyId;
            }

            return null;
        }

        /// <summary>
        /// Helper mapping method from JobVacancy model to JobResponseDto.
        /// </summary>
        private static JobResponseDto MapToResponseDto(JobVacancy job, string companyName)
        {
            return new JobResponseDto
            {
                Id = job.Id,
                CompanyId = job.CompanyId,
                CompanyName = companyName,
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
        }
    }
}
