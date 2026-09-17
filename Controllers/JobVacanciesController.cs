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

            var jobIds = jobs.Select(j => j.Id).ToList();
            var counts = await _dbContext.JobApplications
                .Where(a => jobIds.Contains(a.JobId))
                .GroupBy(a => a.JobId)
                .Select(g => new { JobId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.JobId, x => x.Count);

            var response = jobs.Select(j => MapToResponseDto(j, companyName, counts.TryGetValue(j.Id, out var c) ? c : 0));
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

            var count = await _dbContext.JobApplications.CountAsync(a => a.JobId == id);
            var response = MapToResponseDto(job, job.Company?.CompanyName ?? string.Empty, count);
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
        /// Allows an authenticated candidate to apply for a job vacancy.
        /// Endpoint: POST /api/jobs/{jobId}/apply
        /// </summary>
        [HttpPost("{jobId:guid}/apply")]
        [Authorize]
        [ProducesResponseType(typeof(CandidateApplicationResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ApplyForJob(Guid jobId, [FromBody] ApplyJobDto? dto)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Authentication token is required to apply for jobs." });
            }

            var candidate = await _dbContext.Users.FindAsync(userId.Value);
            if (candidate == null)
            {
                return Unauthorized(new { message = "Candidate profile not found." });
            }

            var job = await _dbContext.JobVacancies
                .Include(j => j.Company)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return NotFound(new { message = $"Job vacancy with ID '{jobId}' was not found." });
            }

            if (job.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "This job vacancy is closed and no longer accepting applications." });
            }

            // Check if already applied
            var existingApp = await _dbContext.JobApplications
                .FirstOrDefaultAsync(a => a.JobId == jobId && a.CandidateId == userId.Value);

            if (existingApp != null)
            {
                return Conflict(new { message = "You have already submitted an application for this position." });
            }

            var application = new JobApplication
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                CandidateId = userId.Value,
                AppliedDate = DateTime.UtcNow,
                Status = "Applied",
                CoverNote = dto?.CoverNote?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.JobApplications.Add(application);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Candidate {CandidateId} ('{CandidateName}') applied for Job {JobId} ('{JobTitle}')", 
                candidate.Id, candidate.FullName, job.Id, job.Title);

            var response = new CandidateApplicationResponseDto
            {
                ApplicationId = application.Id,
                JobId = job.Id,
                JobTitle = job.Title,
                Department = job.Department,
                Location = job.Location,
                EmploymentType = job.EmploymentType,
                CompanyName = job.Company?.CompanyName ?? "Skill Hub Partner",
                CompanyLogoUrl = job.Company?.LogoUrl,
                AppliedDate = application.AppliedDate,
                Status = application.Status
            };

            return StatusCode(StatusCodes.Status201Created, response);
        }

        /// <summary>
        /// Fetches the list of applicants for a specific job vacancy owned by the company.
        /// Endpoint: GET /api/jobs/{jobId}/applications
        /// </summary>
        [HttpGet("{jobId:guid}/applications")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<JobApplicantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetJobApplicants(Guid jobId)
        {
            var job = await _dbContext.JobVacancies
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return Ok(new List<JobApplicantDto>());
            }

            var applications = await _dbContext.JobApplications
                .Where(a => a.JobId == jobId)
                .Include(a => a.Candidate)
                .OrderByDescending(a => a.AppliedDate)
                .ToListAsync();

            var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();

            var skillsMap = await _dbContext.CandidateSkills
                .Where(s => candidateIds.Contains(s.UserId))
                .GroupBy(s => s.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(s => s.SkillName).ToList());

            var eduMap = await _dbContext.CandidateEducations
                .Where(e => candidateIds.Contains(e.UserId))
                .GroupBy(e => e.UserId)
                .ToDictionaryAsync(
                    g => g.Key, 
                    g => g.OrderByDescending(e => e.EndYear).Select(e => $"{e.Degree} • {e.Institution}").FirstOrDefault()
                );

            var candidateExperiences = await _dbContext.CandidateExperiences
                .Where(e => candidateIds.Contains(e.UserId))
                .ToListAsync();

            var currentRoleMap = candidateExperiences
                .GroupBy(e => e.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(e => e.IsCurrent).FirstOrDefault()
                );

            var currentCompanyMap = candidateExperiences
                .Where(e => e.IsCurrent)
                .GroupBy(e => e.UserId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Company).FirstOrDefault());

            var result = applications.Select(app =>
            {
                var candidate = app.Candidate;
                var skills = skillsMap.TryGetValue(app.CandidateId, out var sList) ? sList : new List<string>();
                var highestEdu = eduMap.TryGetValue(app.CandidateId, out var edu) ? edu : null;
                var currentComp = currentCompanyMap.TryGetValue(app.CandidateId, out var comp) ? comp : null;

                var candidateName = !string.IsNullOrWhiteSpace(candidate?.FullName)
                    ? candidate.FullName
                    : $"{candidate?.FirstName} {candidate?.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    candidateName = !string.IsNullOrWhiteSpace(candidate?.Email) ? candidate.Email : "Applicant";
                }

                currentRoleMap.TryGetValue(app.CandidateId, out var recentExp);

                var headline = candidate?.Headline;
                if (string.IsNullOrWhiteSpace(headline))
                {
                    if (recentExp != null && !string.IsNullOrWhiteSpace(recentExp.Title))
                    {
                        headline = !string.IsNullOrWhiteSpace(recentExp.Company)
                            ? $"{recentExp.Title} at {recentExp.Company}"
                            : recentExp.Title;
                    }
                    else if (skills.Count > 0)
                    {
                        headline = $"{string.Join(" • ", skills.Take(2))} Specialist";
                    }
                    else
                    {
                        headline = "Candidate Profile";
                    }
                }

                var location = candidate?.Location;
                if (string.IsNullOrWhiteSpace(location) && recentExp != null && !string.IsNullOrWhiteSpace(recentExp.Location))
                {
                    location = recentExp.Location;
                }

                return new JobApplicantDto
                {
                    ApplicationId = app.Id,
                    JobId = job.Id,
                    JobTitle = job.Title,
                    CandidateId = app.CandidateId,
                    FullName = candidateName,
                    Email = candidate?.Email ?? string.Empty,
                    Phone = candidate?.Phone,
                    Headline = headline,
                    Location = location ?? "Location unspecified",
                    Experience = candidate?.Experience,
                    Availability = candidate?.Availability,
                    AvatarUrl = candidate?.AvatarUrl,
                    About = candidate?.About,
                    AppliedDate = app.AppliedDate,
                    Status = app.Status,
                    CoverNote = app.CoverNote,
                    Skills = skills,
                    HighestEducation = highestEdu,
                    CurrentCompany = currentComp ?? recentExp?.Company
                };
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Checks whether the authenticated candidate has applied to this specific job.
        /// Endpoint: GET /api/jobs/{jobId}/application-status
        /// </summary>
        [HttpGet("{jobId:guid}/application-status")]
        [Authorize]
        [ProducesResponseType(typeof(ApplicationStatusDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApplicationStatus(Guid jobId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Ok(new ApplicationStatusDto { HasApplied = false });
            }

            var app = await _dbContext.JobApplications
                .FirstOrDefaultAsync(a => a.JobId == jobId && a.CandidateId == userId.Value);

            if (app == null)
            {
                return Ok(new ApplicationStatusDto { HasApplied = false });
            }

            return Ok(new ApplicationStatusDto
            {
                HasApplied = true,
                AppliedDate = app.AppliedDate,
                Status = app.Status,
                ApplicationId = app.Id
            });
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
        /// Helper to extract the UserId from the authenticated user's JWT claims.
        /// </summary>
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("id")?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            return null;
        }

        /// <summary>
        /// Helper mapping method from JobVacancy model to JobResponseDto.
        /// </summary>
        private static JobResponseDto MapToResponseDto(JobVacancy job, string companyName, int applicantsCount = 0)
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
                ApplicantsCount = applicantsCount,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            };
        }
    }
}
