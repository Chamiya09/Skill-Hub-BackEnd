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
    [Route("api/candidate/saved-jobs")]
    [Authorize(Roles = "Candidate,CANDIDATE")]
    public class SavedJobsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public SavedJobsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SavedJobDto>>> GetSavedJobs(
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            if (candidateId == null) return Unauthorized();

            var savedJobs = await _dbContext.SavedJobs
                .AsNoTracking()
                .Where(saved => saved.CandidateId == candidateId.Value)
                .OrderByDescending(saved => saved.SavedAt)
                .Select(saved => new SavedJobDto
                {
                    Id = saved.Id,
                    JobId = saved.JobId,
                    JobTitle = saved.Job!.Title,
                    CompanyName = saved.Job.Company != null
                        ? saved.Job.Company.CompanyName
                        : "Enterprise Employer",
                    CompanyLogoUrl = saved.Job.Company != null
                        ? saved.Job.Company.LogoUrl
                        : null,
                    Location = saved.Job.Location,
                    EmploymentType = saved.Job.EmploymentType,
                    ExperienceLevel = saved.Job.ExperienceLevel,
                    SalaryRange = saved.Job.SalaryRange,
                    PostedAt = saved.Job.CreatedAt,
                    SavedAt = saved.SavedAt,
                })
                .ToListAsync(cancellationToken);

            return Ok(savedJobs);
        }

        [HttpGet("ids")]
        public async Task<ActionResult<IReadOnlyList<Guid>>> GetSavedJobIds(
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            if (candidateId == null) return Unauthorized();

            return Ok(await _dbContext.SavedJobs
                .AsNoTracking()
                .Where(saved => saved.CandidateId == candidateId.Value)
                .Select(saved => saved.JobId)
                .ToListAsync(cancellationToken));
        }

        [HttpPut("{jobId:guid}")]
        public async Task<IActionResult> SaveJob(Guid jobId, CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            if (candidateId == null) return Unauthorized();

            var jobExists = await _dbContext.JobVacancies
                .AnyAsync(job => job.Id == jobId && job.Status == "Active", cancellationToken);
            if (!jobExists) return NotFound(new { message = "The active job was not found." });

            var alreadySaved = await _dbContext.SavedJobs.AnyAsync(
                saved => saved.CandidateId == candidateId.Value && saved.JobId == jobId,
                cancellationToken);
            if (!alreadySaved)
            {
                _dbContext.SavedJobs.Add(new SavedJob
                {
                    CandidateId = candidateId.Value,
                    JobId = jobId,
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(new { message = "Job saved successfully.", jobId });
        }

        [HttpDelete("{jobId:guid}")]
        public async Task<IActionResult> RemoveSavedJob(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            var candidateId = GetCandidateId();
            if (candidateId == null) return Unauthorized();

            var savedJob = await _dbContext.SavedJobs.FirstOrDefaultAsync(
                saved => saved.CandidateId == candidateId.Value && saved.JobId == jobId,
                cancellationToken);
            if (savedJob != null)
            {
                _dbContext.SavedJobs.Remove(savedJob);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return NoContent();
        }

        private Guid? GetCandidateId()
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("id");
            return Guid.TryParse(subject, out var candidateId) ? candidateId : null;
        }
    }
}
