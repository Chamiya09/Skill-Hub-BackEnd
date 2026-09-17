using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class ApplicantScreeningService : IApplicantScreeningService
    {
        private const int MaxConcurrentEvaluations = 3;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApplicantScreeningService> _logger;

        public ApplicantScreeningService(
            ApplicationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<ApplicantScreeningService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ScreenedApplicantDto>> FetchAndRankApplicantsAsync(
            Guid jobId,
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            var job = await _dbContext.JobVacancies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    vacancy => vacancy.Id == jobId && vacancy.CompanyId == companyId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Job not found or access denied.");

            var applications = await _dbContext.JobApplications
                .AsNoTracking()
                .AsSplitQuery()
                .Where(application => application.JobId == jobId && application.Status == "Applied")
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Skills)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Experiences)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Projects)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Educations)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Certifications)
                .ToListAsync(cancellationToken);

            var candidateIds = applications.Select(application => application.CandidateId).ToList();
            var cachedScores = await _dbContext.AiMatchResults
                .AsNoTracking()
                .Where(result => result.JobId == jobId && candidateIds.Contains(result.CandidateId))
                .ToDictionaryAsync(result => result.CandidateId, result => result.MatchPercentage, cancellationToken);

            var pending = applications
                .Where(application => application.Candidate is not null &&
                    !cachedScores.ContainsKey(application.CandidateId))
                .Select(application => new EvaluationRequest(
                    application.CandidateId,
                    BuildPayload(application.Candidate!, job)))
                .ToList();

            if (pending.Count > 0)
            {
                using var gate = new SemaphoreSlim(MaxConcurrentEvaluations);
                var client = _httpClientFactory.CreateClient(JobRecommendationService.HttpClientName);
                var evaluations = await Task.WhenAll(pending.Select(async item =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        using var response = await client.PostAsJsonAsync(
                            "api/ai/analyze-match", item.Payload, JsonOptions, cancellationToken);
                        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError(
                                "Python AI evaluation failed for candidate {CandidateId}: {StatusCode} {Body}",
                                item.CandidateId, (int)response.StatusCode, responseBody);
                            response.EnsureSuccessStatusCode();
                        }

                        var result = JsonSerializer.Deserialize<PythonEvaluationResponse>(
                            responseBody, JsonOptions)
                            ?? throw new JsonException("Python AI returned an empty response.");
                        return new CompletedEvaluation(item.CandidateId, result);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));

                // EF Core DbContext is not thread-safe, so HTTP work is concurrent while
                // persistence remains a single unit of work on the request scope.
                foreach (var evaluation in evaluations)
                {
                    var score = Math.Clamp(evaluation.Result.MatchPercentage, 0, 100);
                    _dbContext.AiMatchResults.Add(new AiMatchResult
                    {
                        CandidateId = evaluation.CandidateId,
                        JobId = jobId,
                        MatchPercentage = score,
                        BreakdownJson = evaluation.Result.Breakdown?.GetRawText(),
                        StrengthsJson = JsonSerializer.Serialize(
                            evaluation.Result.Strengths ?? new(), JsonOptions),
                        MissingSkillsJson = JsonSerializer.Serialize(
                            evaluation.Result.MissingSkills ?? new(), JsonOptions),
                        Recommendation = evaluation.Result.Recommendation,
                    });
                    cachedScores[evaluation.CandidateId] = score;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return applications
                .Select(application => MapApplicant(
                    application,
                    cachedScores.TryGetValue(application.CandidateId, out var score) ? score : null))
                .OrderByDescending(candidate => candidate.AiMatchScore ?? -1)
                .ThenBy(candidate => candidate.FullName)
                .ToList();
        }

        private static object BuildPayload(User candidate, JobVacancy job) => new
        {
            candidate_data = new
            {
                candidate.Headline,
                Summary = candidate.About,
                Skills = candidate.Skills.Select(skill => skill.SkillName),
                WorkExperience = candidate.Experiences.Select(item => new
                {
                    item.Title, item.Company, item.StartDate, item.EndDate,
                    item.IsCurrent, item.Description,
                }),
                Projects = candidate.Projects.Select(item => new
                {
                    item.ProjectName, item.Role, item.Description,
                }),
                Education = candidate.Educations.Select(item => new
                {
                    item.Degree, item.Institution, item.FieldOfStudy,
                    item.StartYear, item.EndYear,
                }),
                Certifications = candidate.Certifications.Select(item => new
                {
                    item.Title, item.IssuingOrganization, item.IssueDate,
                }),
            },
            job_data = new
            {
                job.Title, job.Department, job.ExperienceLevel, job.EmploymentType,
                job.Description, job.Location,
            },
        };

        private static ScreenedApplicantDto MapApplicant(JobApplication application, int? score)
        {
            var candidate = application.Candidate;
            return new ScreenedApplicantDto
            {
                ApplicationId = application.Id,
                CandidateId = application.CandidateId,
                FullName = candidate?.FullName ?? candidate?.Email ?? "Candidate",
                Email = candidate?.Email ?? string.Empty,
                Headline = candidate?.Headline,
                Skills = candidate?.Skills.Select(skill => skill.SkillName).ToList() ?? new(),
                AppliedDate = application.AppliedDate,
                Status = application.Status,
                AiMatchScore = score,
            };
        }

        private sealed record EvaluationRequest(Guid CandidateId, object Payload);
        private sealed record CompletedEvaluation(Guid CandidateId, PythonEvaluationResponse Result);
        private sealed record PythonEvaluationResponse(
            [property: JsonPropertyName("MatchPercentage")] int MatchPercentage,
            [property: JsonPropertyName("Strengths")] List<string>? Strengths,
            [property: JsonPropertyName("MissingSkillGaps")] List<string>? MissingSkills,
            [property: JsonPropertyName("AiRecommendation")] string? Recommendation,
            [property: JsonPropertyName("Breakdown")] JsonElement? Breakdown);
    }
}
