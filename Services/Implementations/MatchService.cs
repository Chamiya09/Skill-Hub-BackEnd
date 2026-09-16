using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Ai;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class MatchService : IMatchService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAiAgentService _aiAgentService;

        public MatchService(ApplicationDbContext dbContext, IAiAgentService aiAgentService)
        {
            _dbContext = dbContext;
            _aiAgentService = aiAgentService;
        }

        public async Task<AiMatchResponseDto> AnalyzeAsync(
            Guid candidateId,
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            var candidate = await _dbContext.Users
                .AsNoTracking()
                .AsSplitQuery()
                .Include(user => user.Skills)
                .Include(user => user.Experiences)
                .Include(user => user.Certifications)
                .Include(user => user.Projects)
                .Include(user => user.Educations)
                .Where(user => user.Id == candidateId && user.Role.ToUpper() == "CANDIDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Candidate was not found.");

            var job = await _dbContext.JobVacancies
                .AsNoTracking()
                .Where(vacancy => vacancy.Id == jobId)
                .Select(vacancy => new
                {
                    vacancy.Title,
                    vacancy.Department,
                    vacancy.ExperienceLevel,
                    vacancy.Description,
                })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Job vacancy was not found.");

            // The aggregate was loaded from PostgreSQL above. Empty navigation
            // collections are valid and are intentionally forwarded to Groq.
            var skills = candidate.Skills.Select(skill => skill.SkillName).ToList();
            var experiences = candidate.Experiences
                .OrderByDescending(experience => experience.IsCurrent)
                .ThenByDescending(experience => experience.StartDate)
                .Select(experience => new
                {
                    experience.Title,
                    experience.Company,
                    experience.StartDate,
                    experience.EndDate,
                    experience.IsCurrent,
                    experience.Description,
                })
                .ToList();

            var certifications = candidate.Certifications
                .Select(certification => new
                {
                    certification.Title,
                    certification.IssuingOrganization,
                    certification.IssueDate,
                })
                .ToList();

            var projects = candidate.Projects
                .Select(project => new { project.ProjectName, project.Role, project.Description })
                .ToList();

            var experienceYears = ParseYears(candidate.Experience)
                ?? CalculateYears(experiences.Select(experience =>
                    (experience.StartDate, experience.EndDate, experience.IsCurrent)));

            var request = new AiMatchRequestDto
            {
                Candidate = new CandidateDto
                {
                    Skills = skills.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList(),
                    ExperienceYears = experienceYears,
                    Headline = candidate.Headline,
                    Summary = candidate.About,
                    Experiences = experiences
                        .Select(value => JsonSerializer.SerializeToElement(value)).ToList(),
                    Projects = projects
                        .Select(value => JsonSerializer.SerializeToElement(value)).ToList(),
                    Certifications = certifications
                        .Select(value => JsonSerializer.SerializeToElement(value)).ToList(),
                },
                Job = new JobDto
                {
                    Title = job.Title,
                    Department = job.Department,
                    ExperienceLevel = job.ExperienceLevel,
                    Description = job.Description,
                    // Job requirements are stored in the vacancy description in the
                    // current schema. This stays empty until normalized job skills exist.
                    Skills = new List<string>(),
                },
            };

            return await _aiAgentService.AnalyzeCandidateMatchAsync(request, cancellationToken);
        }

        private static decimal? ParseYears(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var token = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var years)
                ? Math.Clamp(years, 0, 80)
                : null;
        }

        private static decimal CalculateYears(
            IEnumerable<(string StartDate, string? EndDate, bool IsCurrent)> entries)
        {
            var totalDays = entries.Sum(entry =>
            {
                if (!DateTime.TryParse(entry.StartDate, out var start)) return 0d;
                var end = entry.IsCurrent
                    ? DateTime.UtcNow
                    : DateTime.TryParse(entry.EndDate, out var parsedEnd) ? parsedEnd : start;
                return Math.Max(0, (end - start).TotalDays);
            });
            return Math.Round((decimal)(totalDays / 365.25d), 1);
        }
    }
}
