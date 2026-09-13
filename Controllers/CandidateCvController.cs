using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
                Experiences = experiences,
                Educations = educations,
                Projects = projects,
                Skills = skills
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
