using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Recommendations;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class JobRecommendationService : IJobRecommendationService
    {
        public const string HttpClientName = "LangGraphJobRecommendations";

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<JobRecommendationService> _logger;

        public JobRecommendationService(
            ApplicationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<JobRecommendationService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<RecommendedJobResponseDto>> GetRecommendedJobsAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default)
        {
            var candidateExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == candidateId, cancellationToken);

            if (!candidateExists)
            {
                throw new KeyNotFoundException("Candidate was not found.");
            }

            // Bound the batch to protect AI latency and provider rate limits.
            var jobs = await _dbContext.JobVacancies
                .AsNoTracking()
                .Include(job => job.Company)
                .Where(job => job.Status == "Active" && (!job.Deadline.HasValue || job.Deadline.Value >= DateTime.UtcNow))
                .OrderByDescending(job => job.CreatedAt)
                .Take(12)
                .ToListAsync(cancellationToken);

            if (jobs.Count == 0)
            {
                return Array.Empty<RecommendedJobResponseDto>();
            }

            var jobIds = jobs.Select(job => job.Id).ToList();
            var cachedResults = await _dbContext.AiMatchResults
                .AsNoTracking()
                .Where(result =>
                    result.CandidateId == candidateId &&
                    jobIds.Contains(result.JobId))
                .ToDictionaryAsync(
                    result => result.JobId,
                    result => Math.Clamp(result.MatchPercentage, 0, 100),
                    cancellationToken);

            var uncachedJobs = jobs
                .Where(job => !cachedResults.ContainsKey(job.Id))
                .ToList();

            // ── Stale-cache purge ─────────────────────────────────────────────────────
            // A prior broken evaluation (e.g. empty payload causing 0%) is stored via
            // ON CONFLICT DO NOTHING and permanently blocks re-scoring. Purge any 0%
            // rows for this candidate so the next request triggers a fresh AI call.
            var staleZeroJobIds = cachedResults
                .Where(kv => kv.Value == 0)
                .Select(kv => kv.Key)
                .ToList();

            if (staleZeroJobIds.Count > 0)
            {
                _logger.LogInformation(
                    "Purging {Count} stale 0%% cache entries for candidate to force re-evaluation.",
                    staleZeroJobIds.Count);

                foreach (var staleJobId in staleZeroJobIds)
                {
                    await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"""DELETE FROM public."AiMatchResults" WHERE "CandidateId" = {candidateId} AND "JobId" = {staleJobId} AND "MatchPercentage" = 0""",
                        cancellationToken);
                    cachedResults.Remove(staleJobId);
                }

                // Re-compute uncachedJobs after purge so stale entries are re-scored.
                uncachedJobs = jobs
                    .Where(job => !cachedResults.ContainsKey(job.Id))
                    .ToList();
            }

            try
            {
                var skills = new List<string>();
                List<(string Title, string Company, string? StartDate, string? EndDate, bool IsCurrent, string? Description)> experiences = [];
                List<(string ProjectName, string? Role, string? Description)> projects = [];
                List<(string Degree, string? FieldOfStudy, string? StartYear, string? EndYear)> education = [];
                List<(string Title, string IssuingOrganization, string? IssueDate)> certifications = [];

                if (uncachedJobs.Count > 0)
                {
                    // A scoped EF Core DbContext cannot execute concurrent operations.
                    // Keep these lightweight, no-tracking projections sequential.
                    skills = await _dbContext.CandidateSkills
                        .AsNoTracking()
                        .Where(s => s.UserId == candidateId)
                        .Select(s => s.SkillName)
                        .ToListAsync(cancellationToken);

                    var experienceRows = await _dbContext.CandidateExperiences
                        .AsNoTracking()
                        .Where(e => e.UserId == candidateId)
                        .Select(e => new
                        {
                            e.Title,
                            e.Company,
                            e.StartDate,
                            e.EndDate,
                            e.IsCurrent,
                            e.Description,
                        })
                        .ToListAsync(cancellationToken);

                    var projectRows = await _dbContext.CandidateProjects
                        .AsNoTracking()
                        .Where(p => p.UserId == candidateId)
                        .Select(p => new
                        {
                            p.ProjectName,
                            p.Role,
                            p.Description,
                        })
                        .ToListAsync(cancellationToken);

                    var educationRows = await _dbContext.CandidateEducations
                        .AsNoTracking()
                        .Where(e => e.UserId == candidateId)
                        .Select(e => new
                        {
                            e.Degree,
                            e.FieldOfStudy,
                            e.StartYear,
                            e.EndYear,
                        })
                        .ToListAsync(cancellationToken);

                    var certificationRows = await _dbContext.CandidateCertifications
                        .AsNoTracking()
                        .Where(c => c.UserId == candidateId)
                        .Select(c => new
                        {
                            c.Title,
                            c.IssuingOrganization,
                            c.IssueDate,
                        })
                        .ToListAsync(cancellationToken);

                    experiences = experienceRows
                        .Select(e => (e.Title, e.Company, (string?)e.StartDate, (string?)e.EndDate, e.IsCurrent, (string?)e.Description))
                        .ToList();
                    projects = projectRows
                        .Select(p => (p.ProjectName, (string?)p.Role, (string?)p.Description))
                        .ToList();
                    education = educationRows
                        .Select(e => (e.Degree, (string?)e.FieldOfStudy, (string?)e.StartYear, (string?)e.EndYear))
                        .ToList();
                    certifications = certificationRows
                        .Select(c => (c.Title, c.IssuingOrganization, (string?)c.IssueDate))
                        .ToList();

                    // If the candidate has no data at all, skip AI evaluation.
                    if (skills.Count == 0 && experiences.Count == 0 && projects.Count == 0)
                    {
                        uncachedJobs.Clear();
                    }
                }

                if (uncachedJobs.Count > 0)
                {
                    // Build the full candidate digital CV so the LLM can evaluate
                    // all five scoring pillars (Skills, Experience, Projects,
                    // Education, Certifications). Sending a partial DTO caused 0% scores.
                    var candidateCv = new CandidateDigitalCv(
                        Skills: skills,
                        WorkExperience: experiences
                            .Select(e => new CvExperience(
                                e.Title, e.Company, e.StartDate, e.EndDate, e.IsCurrent, e.Description))
                            .ToList(),
                        Projects: projects
                            .Select(p => new CvProject(p.ProjectName, p.Role, p.Description))
                            .ToList(),
                        Education: education
                            .Select(e => new CvEducation(e.Degree, e.FieldOfStudy, e.StartYear, e.EndYear))
                            .ToList(),
                        Certifications: certifications
                            .Select(c => new CvCertification(c.Title, c.IssuingOrganization, c.IssueDate))
                            .ToList()
                    );

                    var payload = new BatchRecommendationRequest(
                        CandidateSkills: skills,
                        CandidateDigitalCv: candidateCv,
                        Jobs: uncachedJobs.Select(job => new BatchJobRequest(
                            job.Id,
                            job.Title,
                            job.Company?.CompanyName ?? "Company",
                            job.Location,
                            new List<string>
                            {
                                job.Title,
                                job.Department,
                                job.ExperienceLevel,
                                job.EmploymentType
                            })).ToList());

                    _logger.LogInformation(
                        "[AI Payload] Sending to Python — Jobs: {JobCount} | Skills: {SkillCount} | " +
                        "Experience: {ExpCount} | Projects: {ProjCount} | Education: {EduCount} | Certs: {CertCount}",
                        uncachedJobs.Count,
                        skills.Count,
                        experiences.Count,
                        projects.Count,
                        education.Count,
                        certifications.Count);

                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    using var response = await client.PostAsJsonAsync(
                        "api/ai/batch-recommend",
                        payload,
                        JsonOptions,
                        cancellationToken);

                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError(
                            "Batch AI endpoint returned {StatusCode}: {ResponseBody}",
                            (int)response.StatusCode,
                            responseBody);
                        response.EnsureSuccessStatusCode();
                    }

                    var scores = JsonSerializer.Deserialize<List<BatchRecommendationResult>>(
                        responseBody,
                        JsonOptions)
                        ?? throw new JsonException("The AI service returned an empty response.");

                    var uncachedJobIds = uncachedJobs.Select(job => job.Id).ToHashSet();
                    var newScores = scores
                        .Where(score => uncachedJobIds.Contains(score.JobId))
                        .GroupBy(score => score.JobId)
                        .Select(group => group.First())
                        .ToList();

                    foreach (var score in newScores)
                    {
                        var cacheId = Guid.NewGuid();
                        var percentage = Math.Clamp(score.MatchPercentage, 0, 100);
                        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $@"INSERT INTO public.""AiMatchResults""
                                (""Id"", ""CandidateId"", ""JobId"", ""MatchPercentage"", ""CreatedAt"")
                               VALUES ({cacheId}, {candidateId}, {score.JobId}, {percentage}, {DateTime.UtcNow})
                               ON CONFLICT (""CandidateId"", ""JobId"") DO NOTHING",
                            cancellationToken);
                    }

                    if (newScores.Count > 0)
                    {
                        var scoredJobIds = newScores.Select(score => score.JobId).ToList();
                        var persistedScores = await _dbContext.AiMatchResults
                            .AsNoTracking()
                            .Where(result =>
                                result.CandidateId == candidateId &&
                                scoredJobIds.Contains(result.JobId))
                            .ToDictionaryAsync(
                                result => result.JobId,
                                result => Math.Clamp(result.MatchPercentage, 0, 100),
                                cancellationToken);

                        foreach (var persistedScore in persistedScores)
                        {
                            cachedResults[persistedScore.Key] = persistedScore.Value;
                        }
                    }
                }

                return BuildRecommendations(jobs, cachedResults);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception,
                    "Batch AI is unavailable; returning jobs with cached advisory scores.");
                return BuildRecommendations(jobs, cachedResults, includeUnscoredJobs: true);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception,
                    "Batch AI returned invalid JSON; returning jobs with cached advisory scores.");
                return BuildRecommendations(jobs, cachedResults, includeUnscoredJobs: true);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException exception)
            {
                _logger.LogWarning(exception,
                    "Batch AI timed out; returning jobs with cached advisory scores.");
                return BuildRecommendations(jobs, cachedResults, includeUnscoredJobs: true);
            }
        }

        private static IReadOnlyList<RecommendedJobResponseDto> BuildRecommendations(
            IReadOnlyCollection<JobVacancy> jobs,
            IReadOnlyDictionary<Guid, int> scores,
            bool includeUnscoredJobs = false)
        {
            return jobs
                .Where(job => includeUnscoredJobs || scores.ContainsKey(job.Id))
                .Select(job =>
                {
                    var percentage = scores.TryGetValue(job.Id, out var score) ? score : 0;
                    return new RecommendedJobResponseDto
                    {
                        JobId = job.Id,
                        Title = job.Title,
                        Company = job.Company?.CompanyName ?? "Company",
                        Location = job.Location,
                        PostedDate = job.CreatedAt,
                        MatchPercentage = percentage,
                        IsRecommended = percentage >= 70,
                    };
                })
                .OrderByDescending(job => job.MatchPercentage)
                .ThenByDescending(job => job.PostedDate)
                .Take(6)
                .ToList();
        }

        // ── Private DTOs (wire contract with the Python LangGraph service) ──────────

        private sealed record BatchRecommendationRequest(
            [property: JsonPropertyName("candidate_skills")] List<string> CandidateSkills,
            [property: JsonPropertyName("candidate_digital_cv")] CandidateDigitalCv CandidateDigitalCv,
            [property: JsonPropertyName("jobs")] List<BatchJobRequest> Jobs);

        private sealed record CandidateDigitalCv(
            [property: JsonPropertyName("skills")] List<string> Skills,
            [property: JsonPropertyName("work_experience")] List<CvExperience> WorkExperience,
            [property: JsonPropertyName("projects")] List<CvProject> Projects,
            [property: JsonPropertyName("education")] List<CvEducation> Education,
            [property: JsonPropertyName("certifications")] List<CvCertification> Certifications);

        private sealed record CvExperience(
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("company")] string Company,
            [property: JsonPropertyName("start_date")] string? StartDate,
            [property: JsonPropertyName("end_date")] string? EndDate,
            [property: JsonPropertyName("is_current")] bool IsCurrent,
            [property: JsonPropertyName("description")] string? Description);

        private sealed record CvProject(
            [property: JsonPropertyName("project_name")] string ProjectName,
            [property: JsonPropertyName("role")] string? Role,
            [property: JsonPropertyName("description")] string? Description);

        private sealed record CvEducation(
            [property: JsonPropertyName("degree")] string Degree,
            [property: JsonPropertyName("field_of_study")] string? FieldOfStudy,
            [property: JsonPropertyName("start_year")] string? StartYear,
            [property: JsonPropertyName("end_year")] string? EndYear);

        private sealed record CvCertification(
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("issuing_organization")] string IssuingOrganization,
            [property: JsonPropertyName("issue_date")] string? IssueDate);

        private sealed record BatchJobRequest(
            [property: JsonPropertyName("job_id")] Guid JobId,
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("company")] string Company,
            [property: JsonPropertyName("location")] string Location,
            [property: JsonPropertyName("requirements")] List<string> Requirements);

        private sealed record BatchRecommendationResult(
            [property: JsonPropertyName("job_id")] Guid JobId,
            [property: JsonPropertyName("match_percentage")] int MatchPercentage,
            [property: JsonPropertyName("is_recommended")] bool IsRecommended);
    }
}
