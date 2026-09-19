using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Assessments;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class AssessmentService : IAssessmentService
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AssessmentService> _logger;
        private readonly IPistonExecutionService _pistonService;

        public AssessmentService(
            ApplicationDbContext dbContext,
            ILogger<AssessmentService> logger,
            IPistonExecutionService pistonService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _pistonService = pistonService;
        }

        #region 1. Assessment Creation & Management

        public async Task<AssessmentResponseDto> CreateAssessmentManualAsync(
            CreateAssessmentManualDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var questionsJson = JsonSerializer.Serialize(dto.Questions, JsonOpts);

            var assessment = new Assessment
            {
                JobVacancyId = dto.JobVacancyId,
                Title = dto.Title.Trim(),
                GeneratedQuestions = "[]",
                FinalQuestions = questionsJson,
                PassingThreshold = dto.PassingThreshold,
                TimeLimitMinutes = dto.TimeLimitMinutes,
                CreatedBy = hrManagerId,
                Status = dto.PublishImmediately ? "Published" : "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Assessments.Add(assessment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToResponseDto(assessment, 0);
        }

        public async Task<AssessmentResponseDto> UpdateAssessmentAsync(
            Guid id,
            UpdateAssessmentDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var assessment = await _dbContext.Assessments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (assessment == null)
                throw new KeyNotFoundException($"Assessment with ID '{id}' was not found.");

            var hasActiveCandidateExam = assessment.Submissions != null &&
                assessment.Submissions.Any(s => s.Status == "Assigned" || s.Status == "Started");

            if (hasActiveCandidateExam)
            {
                throw new InvalidOperationException("This assessment cannot be edited because it has been dispatched to a candidate who has not yet completed the exam.");
            }

            assessment.Title = dto.Title.Trim();
            assessment.PassingThreshold = dto.PassingThreshold;
            assessment.TimeLimitMinutes = dto.TimeLimitMinutes;
            assessment.FinalQuestions = JsonSerializer.Serialize(dto.FinalQuestions, JsonOpts);
            assessment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToResponseDto(assessment, assessment.Submissions?.Count ?? 0);
        }

        public async Task<AssessmentResponseDto> PublishAssessmentAsync(
            Guid id,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            var assessment = await _dbContext.Assessments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (assessment == null)
                throw new KeyNotFoundException($"Assessment with ID '{id}' was not found.");

            assessment.Status = "Published";
            assessment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToResponseDto(assessment, assessment.Submissions.Count);
        }

        public async Task<AssessmentResponseDto> ArchiveAssessmentAsync(
            Guid id,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            var assessment = await _dbContext.Assessments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (assessment == null)
                throw new KeyNotFoundException($"Assessment with ID '{id}' was not found.");

            assessment.Status = "Archived";
            assessment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToResponseDto(assessment, assessment.Submissions.Count);
        }

        public async Task<IReadOnlyList<AssessmentResponseDto>> GetAssessmentsByJobAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default)
        {
            var assessments = await _dbContext.Assessments
                .AsNoTracking()
                .Where(a => a.JobVacancyId == jobVacancyId && a.Status != "Archived")
                .Include(a => a.Submissions)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            return assessments.Select(a => MapToResponseDto(a, a.Submissions.Count)).ToList();
        }

        public async Task<IReadOnlyList<AssessmentTrackSummaryDto>> GetAssessmentTracksByJobAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default)
        {
            var assessments = await _dbContext.Assessments
                .AsNoTracking()
                .Where(a => a.JobVacancyId == jobVacancyId && a.Status == "Published")
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            return assessments.Select(a =>
            {
                var questions = DeserializeQuestions(a.FinalQuestions);
                return new AssessmentTrackSummaryDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    TimeLimitMinutes = a.TimeLimitMinutes,
                    QuestionCount = questions.Count,
                    PassingThreshold = a.PassingThreshold,
                    Status = a.Status
                };
            }).ToList();
        }

        public async Task<AssessmentResponseDto> GetAssessmentByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var assessment = await _dbContext.Assessments
                .AsNoTracking()
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (assessment == null)
                throw new KeyNotFoundException($"Assessment with ID '{id}' was not found.");

            return MapToResponseDto(assessment, assessment.Submissions.Count);
        }

        public async Task<bool> DeleteAssessmentAsync(
            Guid id,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            var assessment = await _dbContext.Assessments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (assessment == null) return false;

            var hasActiveCandidateExam = assessment.Submissions != null &&
                assessment.Submissions.Any(s => s.Status == "Assigned" || s.Status == "Started");

            if (hasActiveCandidateExam)
            {
                throw new InvalidOperationException("This assessment cannot be deleted because it has been dispatched to a candidate who has not yet completed the exam.");
            }

            // If this assessment has candidate submissions (even if completed),
            // soft-delete / archive it so candidates permanently retain their completed assessment records,
            // scorecards, feedback, and interview statuses on their Candidate Dashboard!
            var hasSubmissions = assessment.Submissions != null && assessment.Submissions.Count > 0;
            if (hasSubmissions)
            {
                assessment.Status = "Archived";
                assessment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            _dbContext.Assessments.Remove(assessment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        #endregion

        #region 2. Dispatch Assessment (Incoming from Student 2 / Shortlisted Modal)

        public async Task<DispatchAssessmentResponseDto> DispatchAssessmentAsync(
            DispatchAssessmentRequestDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var assessment = await _dbContext.Assessments
                .FirstOrDefaultAsync(a => a.Id == dto.AssessmentId, cancellationToken);

            if (assessment == null)
                throw new KeyNotFoundException($"Assessment with ID '{dto.AssessmentId}' was not found.");

            var candidate = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == dto.CandidateId, cancellationToken);

            // Look for existing active submission to prevent duplicate invites
            var existingSubmission = await _dbContext.Submissions
                .FirstOrDefaultAsync(s =>
                    s.AssessmentId == dto.AssessmentId &&
                    s.CandidateId == dto.CandidateId &&
                    s.ApplicationId == dto.ApplicationId,
                    cancellationToken);

            Submission submission;
            if (existingSubmission != null)
            {
                submission = existingSubmission;
                submission.CvScore = dto.CvMatchScore;
                submission.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                submission = new Submission
                {
                    AssessmentId = dto.AssessmentId,
                    CandidateId = dto.CandidateId,
                    ApplicationId = dto.ApplicationId,
                    JobVacancyId = dto.JobVacancyId,
                    CvScore = dto.CvMatchScore,
                    ExamScore = 0.00m,
                    FinalWeightedScore = 0.00m,
                    Status = "Assigned",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.Submissions.Add(submission);
            }

            // Also update JobApplication status note or status if needed
            var application = await _dbContext.JobApplications
                .FirstOrDefaultAsync(a => a.Id == dto.ApplicationId, cancellationToken);
            if (application != null)
            {
                application.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var candidateEmail = candidate?.Email ?? "candidate@example.com";

            return new DispatchAssessmentResponseDto
            {
                SubmissionId = submission.Id,
                AssessmentId = assessment.Id,
                AssessmentTitle = assessment.Title,
                CandidateId = dto.CandidateId,
                CandidateEmail = candidateEmail,
                TestLink = $"/exam/take/{submission.Id}",
                ExpiresInHours = 48,
                Status = submission.Status,
                Message = $"Assessment invitation dispatched successfully for {candidateEmail}."
            };
        }

        #endregion

        #region 3. Candidate Examination Flow (Sanitized Questions, Proctoring, Submission)

        public async Task<IReadOnlyList<CandidateAssessmentListItemDto>> GetCandidateAssessmentsAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default)
        {
            var submissions = await _dbContext.Submissions
                .AsNoTracking()
                .Include(s => s.Assessment)
                .Where(s => s.CandidateId == candidateId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            if (submissions.Count == 0)
                return Array.Empty<CandidateAssessmentListItemDto>();

            var jobIds = submissions.Select(s => s.JobVacancyId).Distinct().ToList();
            var jobs = await _dbContext.JobVacancies
                .AsNoTracking()
                .Include(j => j.Company)
                .Where(j => jobIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j, cancellationToken);

            var result = new List<CandidateAssessmentListItemDto>();

            foreach (var s in submissions)
            {
                jobs.TryGetValue(s.JobVacancyId, out var job);
                var assessment = s.Assessment;
                var questions = DeserializeQuestions(assessment?.FinalQuestions);

                result.Add(new CandidateAssessmentListItemDto
                {
                    SubmissionId = s.Id,
                    AssessmentId = s.AssessmentId,
                    AssessmentTitle = assessment?.Title ?? (!string.IsNullOrWhiteSpace(job?.Title) ? $"{job.Title} Skill Assessment" : "Technical Assessment"),
                    JobVacancyId = s.JobVacancyId,
                    JobTitle = job?.Title ?? "Engineering Position",
                    CompanyName = job?.Company?.CompanyName ?? "Hiring Company",
                    Department = job?.Department ?? "Engineering",
                    TimeLimitMinutes = assessment?.TimeLimitMinutes ?? 60,
                    QuestionCount = questions.Count > 0 ? questions.Count : 1,
                    PassingThreshold = assessment?.PassingThreshold ?? 60.00m,
                    Status = s.Status,
                    ExamScore = s.ExamScore,
                    FinalWeightedScore = s.FinalWeightedScore,
                    IsPassed = (s.Status == "Graded" || s.Status == "Passed") && s.ExamScore >= (assessment?.PassingThreshold ?? 60.00m),
                    IsSelectedForInterview = s.IsSelectedForInterview,
                    ReviewerFeedback = s.ReviewerFeedback,
                    AssignedAt = s.CreatedAt,
                    StartedAt = s.StartedAt,
                    SubmittedAt = s.SubmittedAt,
                    ExpiresAt = s.CreatedAt.AddHours(48)
                });
            }

            return result;
        }

        public async Task<StartExamResponseDto> StartExamAsync(
            Guid submissionId,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default)
        {
            var submission = await _dbContext.Submissions
                .Include(s => s.Assessment)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
                throw new KeyNotFoundException($"Submission '{submissionId}' was not found.");

            if (candidateId.HasValue && submission.CandidateId != candidateId.Value)
                throw new UnauthorizedAccessException("You are not authorized to access this exam submission.");

            if (submission.Status == "Assigned")
            {
                submission.Status = "Started";
                submission.StartedAt = DateTime.UtcNow;
                submission.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return BuildSanitizedExamPaper(submission);
        }

        public async Task<StartExamResponseDto> GetExamPaperAsync(
            Guid submissionId,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default)
        {
            var submission = await _dbContext.Submissions
                .AsNoTracking()
                .Include(s => s.Assessment)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
                throw new KeyNotFoundException($"Submission '{submissionId}' was not found.");

            if (candidateId.HasValue && submission.CandidateId != candidateId.Value)
                throw new UnauthorizedAccessException("You are not authorized to access this exam submission.");

            return BuildSanitizedExamPaper(submission);
        }

        public async Task<bool> LogProctorEventAsync(
            Guid submissionId,
            ProctorEventRequestDto eventDto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventDto);

            var submission = await _dbContext.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null) return false;

            var summary = DeserializeProctorSummary(submission.ProctorFlags);
            summary.Events.Add(eventDto);

            if (eventDto.EventType.Equals("TAB_SWITCH", StringComparison.OrdinalIgnoreCase))
                summary.TabSwitches++;
            else if (eventDto.EventType.Equals("WINDOW_BLUR", StringComparison.OrdinalIgnoreCase))
                summary.WindowBlurs++;

            submission.ProctorFlags = JsonSerializer.Serialize(summary, JsonOpts);
            submission.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<RunCodeResponseDto> RunSampleTestAsync(
            Guid submissionId,
            RunCodeRequestDto dto,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var submission = await _dbContext.Submissions
                .AsNoTracking()
                .Include(s => s.Assessment)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
                throw new KeyNotFoundException($"Submission '{submissionId}' was not found.");

            if (candidateId.HasValue && submission.CandidateId != candidateId.Value)
                throw new UnauthorizedAccessException("You are not authorized to run code for this assessment.");

            var questions = DeserializeQuestions(submission.Assessment?.FinalQuestions);
            var question = questions.FirstOrDefault(q => string.Equals(q.Id, dto.QuestionId, StringComparison.OrdinalIgnoreCase));

            string stdin = dto.CustomInput ?? string.Empty;
            string? expectedOutput = null;

            if (string.IsNullOrWhiteSpace(stdin) && question != null && question.SampleTestCases.Count > 0)
            {
                var firstSample = question.SampleTestCases[0];
                stdin = firstSample.Input;
                expectedOutput = firstSample.ExpectedOutput;
            }

            var lang = !string.IsNullOrWhiteSpace(dto.Language) ? dto.Language : (question?.Language ?? "python");
            var result = await _pistonService.ExecuteCodeAsync(
                dto.Code,
                lang,
                stdin,
                cancellationToken);

            bool? samplePassed = null;
            if (!string.IsNullOrWhiteSpace(expectedOutput) && !result.IsError)
            {
                var normalizedActual = result.Stdout.Trim().Replace("\r\n", "\n");
                var normalizedExpected = expectedOutput.Trim().Replace("\r\n", "\n");
                samplePassed = string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal);
            }

            return new RunCodeResponseDto
            {
                Stdout = result.Stdout,
                Stderr = result.Stderr,
                ExitCode = result.ExitCode,
                CompileOutput = result.CompileOutput,
                IsRateLimited = result.IsRateLimited,
                IsError = result.IsError,
                ErrorMessage = result.ErrorMessage,
                ExecutionTimeMs = result.ExecutionTimeMs,
                SampleInputUsed = stdin,
                ExpectedOutput = expectedOutput,
                SamplePassed = samplePassed
            };
        }

        public async Task<SubmissionDetailDto> SubmitAnswersAsync(
            Guid submissionId,
            SubmitAnswersRequestDto answersDto,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(answersDto);

            var submission = await _dbContext.Submissions
                .Include(s => s.Assessment)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
                throw new KeyNotFoundException($"Submission '{submissionId}' was not found.");

            if (candidateId.HasValue && submission.CandidateId != candidateId.Value)
                throw new UnauthorizedAccessException("You are not authorized to submit this exam.");

            var questions = DeserializeQuestions(submission.Assessment?.FinalQuestions);
            var qMap = questions.ToDictionary(q => q.Id, q => q);

            var submittedAnswers = new List<SubmittedAnswerItemDto>();
            decimal totalEarnedPoints = 0;
            decimal totalMaxPoints = 0;
            bool anyAutomatedTested = false;

            foreach (var ans in answersDto.Answers)
            {
                qMap.TryGetValue(ans.QuestionId, out var q);
                var questionMaxPoints = q?.Points ?? 10;
                totalMaxPoints += questionMaxPoints;

                var allTestCases = new List<TestCaseDto>();
                if (q?.SampleTestCases != null) allTestCases.AddRange(q.SampleTestCases);
                if (q?.HiddenTestCases != null) allTestCases.AddRange(q.HiddenTestCases);

                var testCaseEvaluations = new List<TestCaseEvaluationItemDto>();
                int passedCount = 0;
                var lang = !string.IsNullOrWhiteSpace(ans.Language) ? ans.Language : (q?.Language ?? "python");

                if (allTestCases.Count > 0 && !string.IsNullOrWhiteSpace(ans.SubmittedCode))
                {
                    anyAutomatedTested = true;
                    int tcIdx = 1;
                    foreach (var tc in allTestCases)
                    {
                        var execResult = await _pistonService.ExecuteCodeAsync(
                            ans.SubmittedCode,
                            lang,
                            tc.Input,
                            cancellationToken);

                        var normalizedActual = execResult.Stdout.Trim().Replace("\r\n", "\n");
                        var normalizedExpected = tc.ExpectedOutput.Trim().Replace("\r\n", "\n");
                        bool isPassed = !execResult.IsError && string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal);

                        if (isPassed) passedCount++;

                        testCaseEvaluations.Add(new TestCaseEvaluationItemDto
                        {
                            Index = tcIdx++,
                            Input = tc.IsHidden ? "[Hidden Test Case]" : tc.Input,
                            ExpectedOutput = tc.IsHidden ? "[Hidden Test Case]" : tc.ExpectedOutput,
                            ActualOutput = tc.IsHidden && !isPassed ? "[Hidden Test Case - Output Mismatch]" : execResult.Stdout,
                            Passed = isPassed,
                            IsHidden = tc.IsHidden,
                            ErrorMessage = execResult.IsError ? (execResult.CompileOutput ?? execResult.Stderr ?? execResult.ErrorMessage) : null
                        });
                    }
                }

                decimal questionScore = allTestCases.Count > 0
                    ? Math.Round(((decimal)passedCount / allTestCases.Count) * questionMaxPoints, 2)
                    : 0;

                totalEarnedPoints += questionScore;

                submittedAnswers.Add(new SubmittedAnswerItemDto
                {
                    QuestionId = ans.QuestionId,
                    SubmittedCode = ans.SubmittedCode ?? string.Empty,
                    Language = lang,
                    TestCasesPassed = passedCount,
                    TotalTestCases = allTestCases.Count,
                    Score = questionScore,
                    TestCaseResults = testCaseEvaluations
                });
            }

            decimal finalExamScore = totalMaxPoints > 0
                ? Math.Round((totalEarnedPoints / totalMaxPoints) * 100m, 2)
                : 0.00m;

            finalExamScore = Math.Clamp(finalExamScore, 0.00m, 100.00m);
            var passingThreshold = submission.Assessment?.PassingThreshold ?? 60.00m;

            submission.Answers = JsonSerializer.Serialize(submittedAnswers, JsonOpts);
            submission.SubmittedAt = DateTime.UtcNow;
            submission.ExamScore = finalExamScore;
            submission.FinalWeightedScore = finalExamScore;

            if (anyAutomatedTested)
            {
                submission.Status = finalExamScore >= passingThreshold ? "Passed" : "Graded";
                submission.GradedAt = DateTime.UtcNow;
            }
            else
            {
                submission.Status = "Under_Review";
                submission.GradedAt = null;
            }

            submission.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var candidate = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == submission.CandidateId, cancellationToken);

            return MapToSubmissionDetailDto(submission, candidate?.FullName, candidate?.Email, passingThreshold);
        }

        public async Task<SubmissionDetailDto> GetSubmissionDetailAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default)
        {
            var submission = await _dbContext.Submissions
                .AsNoTracking()
                .Include(s => s.Assessment)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
                throw new KeyNotFoundException($"Submission '{submissionId}' was not found.");

            var candidate = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == submission.CandidateId, cancellationToken);
            var threshold = submission.Assessment?.PassingThreshold ?? 60.00m;

            return MapToSubmissionDetailDto(submission, candidate?.FullName, candidate?.Email, threshold);
        }

        public async Task<IReadOnlyList<SubmissionDetailDto>> GetSubmissionsByJobAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default)
        {
            var submissions = await _dbContext.Submissions
                .AsNoTracking()
                .Include(s => s.Assessment)
                .Where(s => s.JobVacancyId == jobVacancyId && s.Status != "Assigned")
                .OrderByDescending(s => s.SubmittedAt ?? s.CreatedAt)
                .ToListAsync(cancellationToken);

            if (submissions.Count == 0)
                return Array.Empty<SubmissionDetailDto>();

            var candidateIds = submissions.Select(s => s.CandidateId).Distinct().ToList();
            var candidates = await _dbContext.Users
                .AsNoTracking()
                .Where(u => candidateIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

            var result = new List<SubmissionDetailDto>();
            foreach (var s in submissions)
            {
                candidates.TryGetValue(s.CandidateId, out var candidate);
                var passingThreshold = s.Assessment?.PassingThreshold ?? 60.00m;
                result.Add(MapToSubmissionDetailDto(s, candidate?.FullName, candidate?.Email, passingThreshold));
            }

            return result;
        }

        public async Task<SubmissionDetailDto> ReviewSubmissionAsync(
            Guid submissionId,
            ManualReviewSubmissionDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var submission = await _dbContext.Submissions
                .Include(s => s.Assessment)
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
                throw new KeyNotFoundException($"Submission '{submissionId}' was not found.");

            var clampedScore = Math.Clamp(dto.ExamScore, 0.00m, 100.00m);
            var passingThreshold = submission.Assessment?.PassingThreshold ?? 60.00m;

            submission.ExamScore = clampedScore;
            submission.FinalWeightedScore = clampedScore;
            submission.IsSelectedForInterview = dto.IsSelectedForInterview;
            submission.ReviewerFeedback = dto.ReviewerFeedback?.Trim();
            submission.ReviewedBy = hrManagerId;
            submission.Status = clampedScore >= passingThreshold ? "Passed" : "Graded";
            submission.GradedAt = DateTime.UtcNow;
            submission.UpdatedAt = DateTime.UtcNow;

            // If question breakdown marks were submitted, update Answers json
            if (dto.QuestionReviews != null && dto.QuestionReviews.Count > 0)
            {
                var existingAnswers = DeserializeAnswers(submission.Answers);
                foreach (var qr in dto.QuestionReviews)
                {
                    var match = existingAnswers.FirstOrDefault(a => string.Equals(a.QuestionId, qr.QuestionId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.Score = qr.PointsEarned;
                        match.TestCasesPassed = qr.IsCorrect ? (match.TotalTestCases > 0 ? match.TotalTestCases : 1) : 0;
                    }
                }
                submission.Answers = JsonSerializer.Serialize(existingAnswers, JsonOpts);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var candidate = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == submission.CandidateId, cancellationToken);

            return MapToSubmissionDetailDto(submission, candidate?.FullName, candidate?.Email, passingThreshold);
        }

        #endregion

        #region 4. Leaderboard & Final Top 5 Promotion (Outgoing Contract to Student 3)

        public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default)
        {
            var submissions = await _dbContext.Submissions
                .AsNoTracking()
                .Where(s => s.JobVacancyId == jobVacancyId && (s.Status == "Passed" || s.Status == "Rejected" || s.Status == "Graded" || s.Status == "Submitted"))
                .OrderByDescending(s => s.FinalWeightedScore)
                .ThenByDescending(s => s.CvScore)
                .ToListAsync(cancellationToken);

            var candidateIds = submissions.Select(s => s.CandidateId).Distinct().ToList();
            var applicationIds = submissions.Select(s => s.ApplicationId).Distinct().ToList();

            var usersMap = await _dbContext.Users
                .AsNoTracking()
                .Where(u => candidateIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);

            var appsMap = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(a => applicationIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            var leaderboard = new List<LeaderboardEntryDto>();
            int rank = 1;

            foreach (var sub in submissions)
            {
                usersMap.TryGetValue(sub.CandidateId, out var user);
                appsMap.TryGetValue(sub.ApplicationId, out var app);

                var proctor = DeserializeProctorSummary(sub.ProctorFlags);

                leaderboard.Add(new LeaderboardEntryDto
                {
                    Rank = rank,
                    SubmissionId = sub.Id,
                    ApplicationId = sub.ApplicationId,
                    CandidateId = sub.CandidateId,
                    CandidateName = user?.FullName ?? user?.Email ?? "Candidate",
                    CandidateEmail = user?.Email ?? string.Empty,
                    CvScore = sub.CvScore,
                    ExamScore = sub.ExamScore,
                    FinalWeightedScore = sub.FinalWeightedScore,
                    SubmissionStatus = sub.Status,
                    ApplicationStatus = app?.Status ?? "Shortlisted",
                    ProctorTabSwitches = proctor.TabSwitches,
                    IsTop5 = rank <= 5 && sub.Status == "Passed",
                    IsPassed = sub.Status == "Passed" || sub.ExamScore >= 60.00m,
                    IsSelectedForInterview = sub.IsSelectedForInterview,
                    SubmittedAt = sub.SubmittedAt
                });

                rank++;
            }

            return leaderboard;
        }

        public async Task<FinalizeTop5ResponseDto> FinalizeTop5Async(
            Guid jobVacancyId,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            // Retrieve all candidate submissions for this job, sorted by FinalWeightedScore descending
            var submissions = await _dbContext.Submissions
                .Where(s => s.JobVacancyId == jobVacancyId && (s.Status == "Passed" || s.Status == "Graded" || s.Status == "Rejected"))
                .OrderByDescending(s => s.FinalWeightedScore)
                .ThenByDescending(s => s.CvScore)
                .ToListAsync(cancellationToken);

            var passedSubmissions = submissions.Where(s => s.Status == "Passed").ToList();
            var top5Submissions = passedSubmissions.Take(5).ToList();
            var top5ApplicationIds = top5Submissions.Select(s => s.ApplicationId).ToHashSet();

            // Fetch related JobApplications for this requisition
            var applications = await _dbContext.JobApplications
                .Where(a => a.JobId == jobVacancyId)
                .ToListAsync(cancellationToken);

            int promotedCount = 0;
            int rejectedCount = 0;

            foreach (var app in applications)
            {
                if (top5ApplicationIds.Contains(app.Id))
                {
                    app.Status = "Assessment Passed";
                    app.UpdatedAt = DateTime.UtcNow;
                    promotedCount++;
                }
                else if (app.Status == "Shortlisted" || app.Status == "Applied")
                {
                    // Candidates who did not pass or make top 5 are marked Rejected per requirements
                    app.Status = "Rejected";
                    app.UpdatedAt = DateTime.UtcNow;
                    rejectedCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Construct strictly-shaped Outgoing Contract payload for Student 3 (Meeting Orchestration)
            var outgoingPayload = top5Submissions.Select(s => new Student3OutgoingCandidateDto
            {
                ApplicationId = s.ApplicationId,
                CandidateId = s.CandidateId,
                JobVacancyId = s.JobVacancyId,
                FinalWeightedScore = s.FinalWeightedScore,
                HrManagerId = hrManagerId
            }).ToList();

            return new FinalizeTop5ResponseDto
            {
                JobVacancyId = jobVacancyId,
                TotalSubmissions = submissions.Count,
                PassedCount = passedSubmissions.Count,
                Top5PromotedCount = promotedCount,
                RejectedCount = rejectedCount,
                OutgoingTop5Payload = outgoingPayload,
                Message = $"Successfully finalized Top {promotedCount} candidate(s) for interview scheduling with Student 3."
            };
        }

        public async Task<bool> DeleteSubmissionAsync(
            Guid submissionId,
            Guid hrManagerId,
            CancellationToken cancellationToken = default)
        {
            var submission = await _dbContext.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null) return false;

            _dbContext.Submissions.Remove(submission);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        #endregion

        #region Helpers: Automated Grading & Serialization

        private static List<SubmittedAnswerItemDto> GradeCodingAnswers(
            List<SubmittedAnswerItemDto> answers,
            List<CodingQuestionItemDto> questions)
        {
            var qMap = questions.ToDictionary(q => q.Id, q => q);
            var graded = new List<SubmittedAnswerItemDto>();

            foreach (var ans in answers)
            {
                qMap.TryGetValue(ans.QuestionId, out var q);
                var totalPoints = q?.Points ?? 10;
                var totalCases = (q?.SampleTestCases.Count ?? 0) + (q?.HiddenTestCases.Count ?? 0);
                if (totalCases == 0) totalCases = 2;

                // Code validation heuristics: Check for non-empty implementation, core logic keywords
                var code = ans.SubmittedCode?.Trim() ?? string.Empty;
                int passedCases = 0;

                if (!string.IsNullOrWhiteSpace(code) && code.Length > 20)
                {
                    // Base points for non-empty syntactic structure
                    passedCases = 1;

                    // Extra points for logic implementation (not just throwing NotImplementedException or returning stub)
                    var isStub = code.Contains("throw new NotImplementedException") ||
                                 code.Contains("pass") && code.Length < 40 ||
                                 code.Contains("return false;") && code.Length < 50;

                    if (!isStub)
                    {
                        // Award proportion based on implementation depth
                        passedCases = totalCases;
                    }
                }

                decimal questionScore = totalCases > 0
                    ? Math.Round(((decimal)passedCases / totalCases) * totalPoints, 2)
                    : 0;

                graded.Add(new SubmittedAnswerItemDto
                {
                    QuestionId = ans.QuestionId,
                    SubmittedCode = ans.SubmittedCode ?? string.Empty,
                    Language = ans.Language,
                    TestCasesPassed = passedCases,
                    TotalTestCases = totalCases,
                    Score = questionScore
                });
            }

            return graded;
        }

        private static StartExamResponseDto BuildSanitizedExamPaper(Submission submission)
        {
            var questions = DeserializeQuestions(submission.Assessment?.FinalQuestions);

            // Sanitize: strip solutionCode, hiddenTestCases, and grading rubrics
            var sanitizedQuestions = questions.Select(q => new CandidateCodingQuestionDto
            {
                Id = q.Id,
                Title = q.Title,
                ProblemStatement = q.ProblemStatement,
                Language = q.Language,
                Difficulty = q.Difficulty,
                StarterCode = q.StarterCode,
                SampleTestCases = q.SampleTestCases.Where(t => !t.IsHidden).ToList(),
                Points = q.Points,
                Order = q.Order
            }).OrderBy(q => q.Order).ToList();

            return new StartExamResponseDto
            {
                SubmissionId = submission.Id,
                AssessmentId = submission.AssessmentId,
                AssessmentTitle = submission.Assessment?.Title ?? "Technical Assessment",
                TimeLimitMinutes = submission.Assessment?.TimeLimitMinutes ?? 60,
                StartedAt = submission.StartedAt ?? DateTime.UtcNow,
                Questions = sanitizedQuestions
            };
        }

        private static AssessmentResponseDto MapToResponseDto(Assessment a, int submissionsCount)
        {
            var hasActiveCandidateExam = a.Submissions != null &&
                a.Submissions.Any(s => s.Status == "Assigned" || s.Status == "Started");

            return new AssessmentResponseDto
            {
                Id = a.Id,
                JobVacancyId = a.JobVacancyId,
                Title = a.Title,
                GeneratedQuestions = DeserializeQuestions(a.GeneratedQuestions),
                FinalQuestions = DeserializeQuestions(a.FinalQuestions),
                PassingThreshold = a.PassingThreshold,
                TimeLimitMinutes = a.TimeLimitMinutes,
                CreatedBy = a.CreatedBy,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                TotalSubmissions = submissionsCount,
                HasActiveCandidateExam = hasActiveCandidateExam,
                CanEdit = !hasActiveCandidateExam
            };
        }

        private static SubmissionDetailDto MapToSubmissionDetailDto(
            Submission s,
            string? candidateName,
            string? candidateEmail,
            decimal threshold)
        {
            List<SubmittedAnswerItemDto> answers = new();
            if (!string.IsNullOrWhiteSpace(s.Answers))
            {
                try { answers = JsonSerializer.Deserialize<List<SubmittedAnswerItemDto>>(s.Answers, JsonOpts) ?? new(); }
                catch { }
            }

            return new SubmissionDetailDto
            {
                Id = s.Id,
                AssessmentId = s.AssessmentId,
                AssessmentTitle = s.Assessment?.Title ?? "Skill Assessment",
                CandidateId = s.CandidateId,
                CandidateName = candidateName,
                CandidateEmail = candidateEmail,
                ApplicationId = s.ApplicationId,
                JobVacancyId = s.JobVacancyId,
                ExamScore = s.ExamScore,
                CvScore = s.CvScore,
                FinalWeightedScore = s.FinalWeightedScore,
                PassingThreshold = threshold,
                Status = s.Status,
                StartedAt = s.StartedAt,
                SubmittedAt = s.SubmittedAt,
                GradedAt = s.GradedAt,
                Answers = answers,
                ProctorSummary = DeserializeProctorSummary(s.ProctorFlags),
                IsSelectedForInterview = s.IsSelectedForInterview,
                ReviewerFeedback = s.ReviewerFeedback
            };
        }

        private static List<SubmittedAnswerItemDto> DeserializeAnswers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<List<SubmittedAnswerItemDto>>(json, JsonOpts) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private static List<CodingQuestionItemDto> DeserializeQuestions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<List<CodingQuestionItemDto>>(json, JsonOpts) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private static ProctorSummaryDto DeserializeProctorSummary(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<ProctorSummaryDto>(json, JsonOpts) ?? new();
            }
            catch
            {
                return new();
            }
        }

        #endregion
    }
}
