using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Candidate;
using Skill_Hub_BackEnd.Models;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/candidate")]
    [Authorize]
    [Produces("application/json")]
    public class CandidateCvController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CandidateCvController> _logger;

        public CandidateCvController(ApplicationDbContext dbContext, ILogger<CandidateCvController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private Guid? GetAuthenticatedUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("id")?.Value
                ?? User.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            return null;
        }

        // ==========================================
        // 1. FULL AGGREGATE CV
        // ==========================================
        /// <summary>
        /// Retrieves the complete Digital CV for the authenticated candidate.
        /// </summary>
        [HttpGet("cv")]
        public async Task<IActionResult> GetFullCv()
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var user = await _dbContext.Users.FindAsync(userId.Value);

            var highlights = new List<CandidateHighlightDto>();
            if (!string.IsNullOrWhiteSpace(user?.KeyHighlights))
            {
                try
                {
                    highlights = JsonSerializer.Deserialize<List<CandidateHighlightDto>>(user.KeyHighlights) ?? new List<CandidateHighlightDto>();
                }
                catch
                {
                    highlights = new List<CandidateHighlightDto>();
                }
            }

            var experiences = await _dbContext.CandidateExperiences
                .Where(e => e.UserId == userId.Value)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new ExperienceDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    Company = e.Company,
                    Location = e.Location,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    IsCurrent = e.IsCurrent,
                    Description = e.Description,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            var educations = await _dbContext.CandidateEducations
                .Where(ed => ed.UserId == userId.Value)
                .OrderByDescending(ed => ed.CreatedAt)
                .Select(ed => new EducationDto
                {
                    Id = ed.Id,
                    Degree = ed.Degree,
                    Institution = ed.Institution,
                    FieldOfStudy = ed.FieldOfStudy,
                    StartYear = ed.StartYear,
                    EndYear = ed.EndYear,
                    Description = ed.Description,
                    CreatedAt = ed.CreatedAt
                })
                .ToListAsync();

            var projects = await _dbContext.CandidateProjects
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    ProjectName = p.ProjectName,
                    Role = p.Role,
                    Description = p.Description,
                    Link = p.Link,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            var skills = await _dbContext.CandidateSkills
                .Where(s => s.UserId == userId.Value)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new SkillDto
                {
                    Id = s.Id,
                    SkillName = s.SkillName,
                    Category = s.Category,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(new CandidateCvDto
            {
                Summary = user?.About,
                KeyHighlights = highlights,
                Experiences = experiences,
                Educations = educations,
                Projects = projects,
                Skills = skills
            });
        }

        // ==========================================
        // 1b. ABOUT & KEY HIGHLIGHTS ENDPOINTS
        // ==========================================
        /// <summary>
        /// Retrieves the About summary and key highlights for the candidate.
        /// </summary>
        [HttpGet("about")]
        public async Task<IActionResult> GetAbout()
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null) return NotFound(new { message = "Candidate profile not found." });

            var highlights = new List<CandidateHighlightDto>();
            if (!string.IsNullOrWhiteSpace(user.KeyHighlights))
            {
                try
                {
                    highlights = JsonSerializer.Deserialize<List<CandidateHighlightDto>>(user.KeyHighlights) ?? new List<CandidateHighlightDto>();
                }
                catch
                {
                    highlights = new List<CandidateHighlightDto>();
                }
            }

            return Ok(new CandidateAboutDto
            {
                Summary = user.About,
                KeyHighlights = highlights
            });
        }

        /// <summary>
        /// Updates the candidate's About summary and up to 3 key highlights.
        /// Endpoint: PUT /api/candidate/about
        /// </summary>
        [HttpPut("about")]
        public async Task<IActionResult> UpdateAbout([FromBody] UpdateCandidateAboutDto dto)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null) return NotFound(new { message = "Candidate profile not found." });

            user.About = dto.Summary;

            var cleanHighlights = new List<CandidateHighlightDto>();
            if (dto.KeyHighlights != null && dto.KeyHighlights.Count > 0)
            {
                cleanHighlights = dto.KeyHighlights
                    .Where(h => !string.IsNullOrWhiteSpace(h.Category) || !string.IsNullOrWhiteSpace(h.Value) || !string.IsNullOrWhiteSpace(h.Subtext))
                    .Take(3)
                    .Select(h => new CandidateHighlightDto
                    {
                        Category = h.Category?.Trim() ?? string.Empty,
                        Value = h.Value?.Trim() ?? string.Empty,
                        Subtext = h.Subtext?.Trim() ?? string.Empty
                    })
                    .ToList();
            }

            user.KeyHighlights = JsonSerializer.Serialize(cleanHighlights);
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated About summary and {Count} KeyHighlights for candidate user {UserId}", cleanHighlights.Count, userId.Value);

            return Ok(new CandidateAboutDto
            {
                Summary = user.About,
                KeyHighlights = cleanHighlights
            });
        }

        // ==========================================
        // 2. EXPERIENCE ENDPOINTS
        // ==========================================
        [HttpGet("experience")]
        public async Task<IActionResult> GetExperiences()
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var list = await _dbContext.CandidateExperiences
                .Where(e => e.UserId == userId.Value)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new ExperienceDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    Company = e.Company,
                    Location = e.Location,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    IsCurrent = e.IsCurrent,
                    Description = e.Description,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("experience")]
        public async Task<IActionResult> AddExperience([FromBody] CreateExperienceDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var experience = new CandidateExperience
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Title = dto.Title.Trim(),
                Company = dto.Company.Trim(),
                Location = dto.Location?.Trim(),
                StartDate = dto.StartDate.Trim(),
                EndDate = dto.IsCurrent ? null : dto.EndDate?.Trim(),
                IsCurrent = dto.IsCurrent,
                Description = dto.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.CandidateExperiences.Add(experience);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added experience '{Title}' at '{Company}' for candidate user {UserId}", experience.Title, experience.Company, userId.Value);

            return StatusCode(StatusCodes.Status201Created, new ExperienceDto
            {
                Id = experience.Id,
                Title = experience.Title,
                Company = experience.Company,
                Location = experience.Location,
                StartDate = experience.StartDate,
                EndDate = experience.EndDate,
                IsCurrent = experience.IsCurrent,
                Description = experience.Description,
                CreatedAt = experience.CreatedAt
            });
        }

        [HttpDelete("experience/{id:guid}")]
        public async Task<IActionResult> DeleteExperience(Guid id)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var item = await _dbContext.CandidateExperiences.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId.Value);
            if (item == null) return NotFound(new { message = "Experience entry not found." });

            _dbContext.CandidateExperiences.Remove(item);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Experience deleted successfully." });
        }

        [HttpPut("experience/{id:guid}")]
        public async Task<IActionResult> UpdateExperience(Guid id, [FromBody] CreateExperienceDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var experience = await _dbContext.CandidateExperiences.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId.Value);
            if (experience == null) return NotFound(new { message = "Experience entry not found." });

            experience.Title = dto.Title.Trim();
            experience.Company = dto.Company.Trim();
            experience.Location = dto.Location?.Trim();
            experience.StartDate = dto.StartDate.Trim();
            experience.EndDate = dto.IsCurrent ? null : dto.EndDate?.Trim();
            experience.IsCurrent = dto.IsCurrent;
            experience.Description = dto.Description?.Trim();
            experience.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated experience {Id} for candidate user {UserId}", id, userId.Value);

            return Ok(new ExperienceDto
            {
                Id = experience.Id,
                Title = experience.Title,
                Company = experience.Company,
                Location = experience.Location,
                StartDate = experience.StartDate,
                EndDate = experience.EndDate,
                IsCurrent = experience.IsCurrent,
                Description = experience.Description,
                CreatedAt = experience.CreatedAt
            });
        }

        // ==========================================
        // 3. EDUCATION ENDPOINTS
        // ==========================================
        [HttpGet("education")]
        public async Task<IActionResult> GetEducations()
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var list = await _dbContext.CandidateEducations
                .Where(ed => ed.UserId == userId.Value)
                .OrderByDescending(ed => ed.CreatedAt)
                .Select(ed => new EducationDto
                {
                    Id = ed.Id,
                    Degree = ed.Degree,
                    Institution = ed.Institution,
                    FieldOfStudy = ed.FieldOfStudy,
                    StartYear = ed.StartYear,
                    EndYear = ed.EndYear,
                    Description = ed.Description,
                    CreatedAt = ed.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("education")]
        public async Task<IActionResult> AddEducation([FromBody] CreateEducationDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var education = new CandidateEducation
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Degree = dto.Degree.Trim(),
                Institution = dto.Institution.Trim(),
                FieldOfStudy = dto.FieldOfStudy?.Trim(),
                StartYear = dto.StartYear.Trim(),
                EndYear = dto.EndYear?.Trim(),
                Description = dto.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.CandidateEducations.Add(education);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added education '{Degree}' from '{Institution}' for candidate user {UserId}", education.Degree, education.Institution, userId.Value);

            return StatusCode(StatusCodes.Status201Created, new EducationDto
            {
                Id = education.Id,
                Degree = education.Degree,
                Institution = education.Institution,
                FieldOfStudy = education.FieldOfStudy,
                StartYear = education.StartYear,
                EndYear = education.EndYear,
                Description = education.Description,
                CreatedAt = education.CreatedAt
            });
        }

        [HttpDelete("education/{id:guid}")]
        public async Task<IActionResult> DeleteEducation(Guid id)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var item = await _dbContext.CandidateEducations.FirstOrDefaultAsync(ed => ed.Id == id && ed.UserId == userId.Value);
            if (item == null) return NotFound(new { message = "Education entry not found." });

            _dbContext.CandidateEducations.Remove(item);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Education deleted successfully." });
        }

        [HttpPut("education/{id:guid}")]
        public async Task<IActionResult> UpdateEducation(Guid id, [FromBody] CreateEducationDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var education = await _dbContext.CandidateEducations.FirstOrDefaultAsync(ed => ed.Id == id && ed.UserId == userId.Value);
            if (education == null) return NotFound(new { message = "Education entry not found." });

            education.Degree = dto.Degree.Trim();
            education.Institution = dto.Institution.Trim();
            education.FieldOfStudy = dto.FieldOfStudy?.Trim();
            education.StartYear = dto.StartYear.Trim();
            education.EndYear = dto.EndYear?.Trim();
            education.Description = dto.Description?.Trim();
            education.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated education {Id} for candidate user {UserId}", id, userId.Value);

            return Ok(new EducationDto
            {
                Id = education.Id,
                Degree = education.Degree,
                Institution = education.Institution,
                FieldOfStudy = education.FieldOfStudy,
                StartYear = education.StartYear,
                EndYear = education.EndYear,
                Description = education.Description,
                CreatedAt = education.CreatedAt
            });
        }

        // ==========================================
        // 4. PROJECT ENDPOINTS
        // ==========================================
        [HttpGet("project")]
        public async Task<IActionResult> GetProjects()
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var list = await _dbContext.CandidateProjects
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    ProjectName = p.ProjectName,
                    Role = p.Role,
                    Description = p.Description,
                    Link = p.Link,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("project")]
        public async Task<IActionResult> AddProject([FromBody] CreateProjectDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var project = new CandidateProject
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                ProjectName = dto.ProjectName.Trim(),
                Role = dto.Role?.Trim(),
                Description = dto.Description?.Trim(),
                Link = dto.Link?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.CandidateProjects.Add(project);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added project '{ProjectName}' for candidate user {UserId}", project.ProjectName, userId.Value);

            return StatusCode(StatusCodes.Status201Created, new ProjectDto
            {
                Id = project.Id,
                ProjectName = project.ProjectName,
                Role = project.Role,
                Description = project.Description,
                Link = project.Link,
                CreatedAt = project.CreatedAt
            });
        }

        [HttpDelete("project/{id:guid}")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var item = await _dbContext.CandidateProjects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);
            if (item == null) return NotFound(new { message = "Project entry not found." });

            _dbContext.CandidateProjects.Remove(item);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Project deleted successfully." });
        }

        [HttpPut("project/{id:guid}")]
        public async Task<IActionResult> UpdateProject(Guid id, [FromBody] CreateProjectDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var project = await _dbContext.CandidateProjects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value);
            if (project == null) return NotFound(new { message = "Project entry not found." });

            project.ProjectName = dto.ProjectName.Trim();
            project.Role = dto.Role?.Trim();
            project.Description = dto.Description?.Trim();
            project.Link = dto.Link?.Trim();
            project.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated project {Id} for candidate user {UserId}", id, userId.Value);

            return Ok(new ProjectDto
            {
                Id = project.Id,
                ProjectName = project.ProjectName,
                Role = project.Role,
                Description = project.Description,
                Link = project.Link,
                CreatedAt = project.CreatedAt
            });
        }

        // ==========================================
        // 5. SKILL ENDPOINTS
        // ==========================================
        [HttpGet("skill")]
        public async Task<IActionResult> GetSkills()
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var list = await _dbContext.CandidateSkills
                .Where(s => s.UserId == userId.Value)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new SkillDto
                {
                    Id = s.Id,
                    SkillName = s.SkillName,
                    Category = s.Category,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("skill")]
        public async Task<IActionResult> AddSkill([FromBody] CreateSkillDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var skill = new CandidateSkill
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                SkillName = dto.SkillName.Trim(),
                Category = dto.Category?.Trim() ?? "General",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.CandidateSkills.Add(skill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added skill '{SkillName}' for candidate user {UserId}", skill.SkillName, userId.Value);

            return StatusCode(StatusCodes.Status201Created, new SkillDto
            {
                Id = skill.Id,
                SkillName = skill.SkillName,
                Category = skill.Category,
                CreatedAt = skill.CreatedAt
            });
        }

        [HttpDelete("skill/{id:guid}")]
        public async Task<IActionResult> DeleteSkill(Guid id)
        {
            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "Invalid authentication token." });

            var item = await _dbContext.CandidateSkills.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value);
            if (item == null) return NotFound(new { message = "Skill entry not found." });

            _dbContext.CandidateSkills.Remove(item);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Skill deleted successfully." });
        }
    }
}
