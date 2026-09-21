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
        private readonly IAgenticCvService _agenticCvService;
        private readonly ILogger<ApplicantScreeningService> _logger;

        public ApplicantScreeningService(
            ApplicationDbContext dbContext,
            IAgenticCvService agenticCvService,
            ILogger<ApplicantScreeningService> logger)
        {
            _dbContext = dbContext;
            _agenticCvService = agenticCvService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ScreenedApplicantDto>> FetchAndRankApplicantsAsync(
            Guid jobId,
            Guid companyId,
            bool forceRefresh = false,
            bool runAiAnalysis = true,
            CancellationToken cancellationToken = default)
        {
            var job = await _dbContext.JobVacancies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    vacancy => vacancy.Id == jobId && vacancy.CompanyId == companyId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Job not found or access denied.");

            // Candidates whose assessments have already been reviewed through the Performance Hub are automatically excluded from AI Screening
            var reviewedCandidateIds = await _dbContext.Submissions
                .AsNoTracking()
                .Where(s => s.JobVacancyId == jobId &&
                            (s.ReviewedBy != null || s.GradedAt != null || s.Status == "Graded" || s.Status == "Passed" || s.IsSelectedForInterview))
                .Select(s => s.CandidateId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var applications = await _dbContext.JobApplications
                .AsNoTracking()
                .AsSplitQuery()
                .Where(application => application.JobId == jobId &&
                                      !reviewedCandidateIds.Contains(application.CandidateId) &&
                                      application.Status != "Assessment_Reviewed" &&
                                      application.Status != "Interview" &&
                                      application.Status != "Rejected" &&
                                      (application.Status == "Applied" || application.Status == "Shortlisted" || application.Status == "Assessment"))
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Skills)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Experiences)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Projects)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Educations)
                .Include(application => application.Candidate)!.ThenInclude(candidate => candidate!.Certifications)
                .ToListAsync(cancellationToken);

            var candidateIds = applications.Select(application => application.CandidateId).ToList();

            // ── forceRefresh: wipe all cached results so every candidate is re-evaluated ──
            if (forceRefresh)
            {
                var staleResults = await _dbContext.CvEvaluationResults
                    .Where(result => result.JobId == jobId && candidateIds.Contains(result.CandidateId))
                    .ToListAsync(cancellationToken);

                if (staleResults.Count > 0)
                {
                    _dbContext.CvEvaluationResults.RemoveRange(staleResults);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "forceRefresh=true: deleted {Count} cached CV evaluation result(s) for job {JobId}.",
                        staleResults.Count, jobId);
                }
            }

            // ── Load remaining cached results (empty after forceRefresh) ──
            var cachedResults = await _dbContext.CvEvaluationResults
                .AsNoTracking()
                .Where(result => result.JobId == jobId && candidateIds.Contains(result.CandidateId))
                .ToListAsync(cancellationToken);

            // Build lookup dictionaries for score
            var cachedScores = cachedResults.ToDictionary(r => r.CandidateId, r => r.MatchScore);

            if (runAiAnalysis)
            {
                var candidatesToAnalyze = applications.Where(a => !cachedScores.ContainsKey(a.CandidateId)).ToList();
                
                foreach (var application in candidatesToAnalyze)
                {
                    try
                    {
                        var result = await _agenticCvService.AnalyzeCvAsync(
                            new DTOs.CvEvaluation.AnalyzeCvRequestDto
                            {
                                CandidateId = application.CandidateId,
                                JobId = jobId,
                                ApplicationId = application.Id,
                                ForceRefresh = false
                            },
                            companyId,
                            cancellationToken);
                            
                        cachedScores[application.CandidateId] = result.MatchScore;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to analyze CV for candidate {CandidateId}", application.CandidateId);
                    }
                }
            }

            return applications
                .Select(application => MapApplicant(
                    application,
                    cachedScores.TryGetValue(application.CandidateId, out var score) ? score : null,
                    null))
                .OrderByDescending(candidate => candidate.AiMatchScore ?? -1)
                .ThenBy(candidate => candidate.FullName)
                .ToList();
        }

        /// <summary>
        /// Safely deserializes the stored JSON breakdown into a <see cref="ScoreBreakdown"/>.
        /// Returns null if the JSON is missing or malformed so a failed parse never crashes a response.
        /// </summary>
        private ScoreBreakdown? ParseBreakdown(string breakdownJson)
        {
            try
            {
                return JsonSerializer.Deserialize<ScoreBreakdown>(breakdownJson, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not deserialize BreakdownJson: {Json}", breakdownJson);
                return null;
            }
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

        private static ScreenedApplicantDto MapApplicant(
            JobApplication application,
            int? score,
            ScoreBreakdown? breakdown)
        {
            var candidate = application.Candidate;
            return new ScreenedApplicantDto
            {
                ApplicationId = application.Id,
                CandidateId = application.CandidateId,
                FullName = candidate?.FullName ?? candidate?.Email ?? "Candidate",
                Email = candidate?.Email ?? string.Empty,
                Headline = candidate?.Headline,
                Location = candidate?.Location,
                Phone = candidate?.Phone,
                Skills = candidate?.Skills?.Select(s => s.SkillName).ToList() ?? new List<string>(),
                AppliedDate = application.AppliedDate,
                Status = application.Status,
                AiMatchScore = score
            };
        }
    }
}
