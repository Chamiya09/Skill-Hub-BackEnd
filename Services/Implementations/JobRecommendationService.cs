using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Recommendations;
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
                .Where(job => job.Status == "Active")
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

            try
            {
                var skills = new List<string>();
                if (uncachedJobs.Count > 0)
                {
                    skills = await _dbContext.CandidateSkills
                        .AsNoTracking()
                        .Where(skill => skill.UserId == candidateId)
                        .Select(skill => skill.SkillName)
                        .ToListAsync(cancellationToken);

                    if (skills.Count == 0)
                    {
                        uncachedJobs.Clear();
                    }
                }

                if (uncachedJobs.Count > 0)
                {
                    var payload = new BatchRecommendationRequest(
                        skills,
                        uncachedJobs.Select(job => new BatchJobRequest(
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

                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    using var response = await client.PostAsJsonAsync(
                        "api/ai/batch-recommend",
                        payload,
                        JsonOptions,
                        cancellationToken);

                    response.EnsureSuccessStatusCode();

                    var scores = await response.Content.ReadFromJsonAsync<List<BatchRecommendationResult>>(
                        JsonOptions,
                        cancellationToken)
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

                var jobsById = jobs.ToDictionary(job => job.Id);

                return cachedResults
                    .Where(result => jobsById.ContainsKey(result.Key))
                    .Select(result =>
                    {
                        var job = jobsById[result.Key];
                        var percentage = result.Value;
                        return new RecommendedJobResponseDto
                        {
                            JobId = job.Id,
                            Title = job.Title,
                            Company = job.Company?.CompanyName ?? "Company",
                            Location = job.Location,
                            PostedDate = job.CreatedAt,
                            MatchPercentage = percentage,
                            IsRecommended = percentage >= 70
                        };
                    })
                    .OrderByDescending(job => job.MatchPercentage)
                    .ThenByDescending(job => job.PostedDate)
                    .Take(6)
                    .ToList();
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "The LangGraph recommendation service is unavailable.");
                throw new InvalidOperationException(
                    "The job recommendation service is currently unavailable.",
                    exception);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "The LangGraph recommendation response was invalid.");
                throw new InvalidOperationException(
                    "The job recommendation service returned an invalid response.",
                    exception);
            }
        }

        private sealed record BatchRecommendationRequest(
            [property: JsonPropertyName("candidate_skills")] List<string> CandidateSkills,
            [property: JsonPropertyName("jobs")] List<BatchJobRequest> Jobs);

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
