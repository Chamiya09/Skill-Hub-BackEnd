using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.InterviewPrep;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    /// <summary>
    /// Implementation of the AI Interview Preparation Guide Service (Student 1 module).
    /// End-to-end data pipeline:
    /// 1. Validates candidate eligibility (strictly 'Interview' stage).
    /// 2. Calls the Python FastAPI microservice (Groq AI / temperature=0.0) with job description.
    /// 3. Saves the real structured AI study guidelines into PostgreSQL (InterviewPrepGuide entity).
    /// 4. Serves real persisted study data to the React frontend Study Dashboard.
    /// </summary>
    public sealed class InterviewPrepService : IInterviewPrepService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPythonInterviewPrepClient _pythonClient;
        private readonly ILogger<InterviewPrepService> _logger;

        public InterviewPrepService(
            ApplicationDbContext dbContext,
            IPythonInterviewPrepClient pythonClient,
            ILogger<InterviewPrepService> logger)
        {
            _dbContext = dbContext;
            _pythonClient = pythonClient;
            _logger = logger;
        }

        public async Task<InterviewPrepGuideDto> GenerateGuideAsync(
            Guid candidateId,
            GenerateInterviewPrepRequestDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Generating Real AI Study Guidelines for Candidate {CandidateId}, AppId: {AppId}, JobId: {JobId}, Title: {JobTitle}",
                candidateId, request.ApplicationId, request.JobId, request.JobTitle);

            // ── Strict Stage Gate: ONLY 'Interview' Stage Permitted ─────────────────
            var eligibility = await CheckEligibilityAsync(
                candidateId,
                request.ApplicationId,
                request.JobId,
                cancellationToken);

            if (!eligibility.IsEligible)
            {
                _logger.LogWarning(
                    "Interview prep access denied for Candidate {CandidateId}. Current stage: {Status}",
                    candidateId, eligibility.ApplicationStatus);

                throw new InterviewPrepIneligibleException(
                    "Access denied. Interview preparation is strictly available only when your application reaches the 'Interview' stage.",
                    403,
                    eligibility.ApplicationStatus);
            }

            // Fetch job title/description and company from DB if ApplicationId or JobId provided
            string resolvedTitle = request.JobTitle ?? string.Empty;
            string resolvedDescription = request.JobDescription ?? string.Empty;

            string? resolvedCompany = null;
            string? resolvedLocation = null;
            string? resolvedEmploymentType = null;
            string? resolvedStatus = null;
            DateTime? resolvedAppliedDate = null;

            if (request.ApplicationId.HasValue)
            {
                var app = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Include(a => a.Job)
                        .ThenInclude(j => j!.Company)
                    .FirstOrDefaultAsync(a => a.Id == request.ApplicationId.Value, cancellationToken);

                if (app != null)
                {
                    request.JobId ??= app.JobId;
                    resolvedStatus = app.Status;
                    resolvedAppliedDate = app.AppliedDate;
                    if (string.IsNullOrWhiteSpace(resolvedTitle) && app.Job != null)
                    {
                        resolvedTitle = app.Job.Title;
                    }
                    if (string.IsNullOrWhiteSpace(resolvedDescription) && app.Job != null)
                    {
                        resolvedDescription = app.Job.Description;
                    }
                    if (app.Job != null)
                    {
                        resolvedCompany = app.Job.Company?.CompanyName;
                        resolvedLocation = app.Job.Location;
                        resolvedEmploymentType = app.Job.EmploymentType;
                    }
                }
            }

            if (request.JobId.HasValue && (string.IsNullOrWhiteSpace(resolvedTitle) || string.IsNullOrWhiteSpace(resolvedDescription) || string.IsNullOrWhiteSpace(resolvedCompany)))
            {
                var job = await _dbContext.JobVacancies
                    .AsNoTracking()
                    .Include(j => j.Company)
                    .FirstOrDefaultAsync(j => j.Id == request.JobId.Value, cancellationToken);

                if (job != null)
                {
                    if (string.IsNullOrWhiteSpace(resolvedTitle)) resolvedTitle = job.Title;
                    if (string.IsNullOrWhiteSpace(resolvedDescription)) resolvedDescription = job.Description;
                    resolvedCompany ??= job.Company?.CompanyName;
                    resolvedLocation ??= job.Location;
                    resolvedEmploymentType ??= job.EmploymentType;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedTitle))
            {
                resolvedTitle = !string.IsNullOrWhiteSpace(request.TargetRole) 
                    ? request.TargetRole 
                    : "Full-Stack Software Engineer";
            }

            var targetRole = !string.IsNullOrWhiteSpace(request.TargetRole) 
                ? request.TargetRole 
                : resolvedTitle;

            var companyName = resolvedCompany ?? "Enterprise Partner";

            // ── Call Python AI Agent Microservice (Groq / temperature=0.0) ──────────
            var pythonRequest = new PythonGenerateGuideRequest
            {
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                JobDescription = !string.IsNullOrWhiteSpace(resolvedDescription) 
                    ? resolvedDescription 
                    : $"Role: {resolvedTitle} at {companyName}. Core requirements include enterprise software architecture, high availability, and hands-on system implementation.",
                ExperienceLevel = request.ExperienceLevel ?? "Mid-Senior",
                CompanyName = companyName
            };

            _logger.LogInformation(
                "Forwarding job description to Python AI microservice for role '{Role}' at '{Company}'...",
                targetRole, companyName);

            var aiResponse = await _pythonClient.GenerateGuideAsync(pythonRequest, cancellationToken);

            // Map real AI-generated sections
            var keyTheoreticalAreas = (aiResponse.KeyTheoreticalAreas ?? new List<PythonStudyFocusArea>())
                .Select(MapFromPython)
                .ToList();

            var practicalImplementationFocus = (aiResponse.PracticalImplementationFocus ?? new List<PythonStudyFocusArea>())
                .Select(MapFromPython)
                .ToList();

            var proTips = aiResponse.ProTips ?? new List<string>();
            var checklist = aiResponse.PreparationChecklist ?? new List<string>();
            var roleOverview = !string.IsNullOrWhiteSpace(aiResponse.RoleOverviewSummary)
                ? aiResponse.RoleOverviewSummary
                : $"Technical Career Coach Guideline for {targetRole} at {companyName}.";

            var studyStore = new StudyGuidelineStore
            {
                KeyTheoreticalAreas = keyTheoreticalAreas,
                TechnicalCoreConcepts = new List<StudyFocusAreaDto>(),
                PracticalImplementationFocus = practicalImplementationFocus,
                ProTips = proTips,
                PreparationChecklist = checklist
            };

            var candidateExists = await _dbContext.Users.AnyAsync(u => u.Id == candidateId, cancellationToken);

            var guideEntity = new InterviewPrepGuide
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateExists ? candidateId : null,
                ApplicationId = request.ApplicationId,
                JobId = request.JobId,
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                JobDescription = resolvedDescription,
                RoleOverviewSummary = roleOverview,
                TechnicalQuestionsJson = JsonSerializer.Serialize(studyStore),
                BehavioralQuestionsJson = JsonSerializer.Serialize(practicalImplementationFocus),
                ProTipsJson = JsonSerializer.Serialize(proTips),
                ChecklistJson = JsonSerializer.Serialize(checklist),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.InterviewPrepGuides.Add(guideEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Saved real AI InterviewPrepGuide {GuideId} to PostgreSQL database.", guideEntity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist InterviewPrepGuide to database: {Message}", ex.Message);
                throw;
            }

            return new InterviewPrepGuideDto
            {
                Id = guideEntity.Id,
                CandidateId = guideEntity.CandidateId ?? candidateId,
                ApplicationId = guideEntity.ApplicationId,
                JobId = request.JobId,
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                CompanyName = companyName,
                Location = resolvedLocation,
                EmploymentType = resolvedEmploymentType,
                ApplicationStatus = resolvedStatus ?? "Interview",
                AppliedDate = resolvedAppliedDate,
                InterviewDate = resolvedAppliedDate?.AddDays(7),
                JobDescription = resolvedDescription,
                RoleOverviewSummary = roleOverview,
                KeyTheoreticalAreas = keyTheoreticalAreas,
                TechnicalCoreConcepts = new List<StudyFocusAreaDto>(),
                PracticalImplementationFocus = practicalImplementationFocus,
                ProTips = proTips,
                PreparationChecklist = checklist,
                CreatedAt = guideEntity.CreatedAt
            };
        }

        public async Task<InterviewPrepGuideDto?> GetGuideByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.InterviewPrepGuides
                .AsNoTracking()
                .Include(g => g.JobApplication)
                    .ThenInclude(a => a!.Job)
                        .ThenInclude(j => j!.Company)
                .Include(g => g.Job)
                    .ThenInclude(j => j!.Company)
                .FirstOrDefaultAsync(g => g.Id == id || g.ApplicationId == id, cancellationToken);

            if (entity == null)
            {
                var app = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Include(a => a.Job)
                        .ThenInclude(j => j!.Company)
                    .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

                if (app != null)
                {
                    entity = await _dbContext.InterviewPrepGuides
                        .AsNoTracking()
                        .Include(g => g.JobApplication)
                            .ThenInclude(a => a!.Job)
                                .ThenInclude(j => j!.Company)
                        .Include(g => g.Job)
                            .ThenInclude(j => j!.Company)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync(g => g.ApplicationId == app.Id || (g.CandidateId == app.CandidateId && g.JobId == app.JobId), cancellationToken);

                    if (entity == null && (app.Status ?? "").Equals("Interview", StringComparison.OrdinalIgnoreCase))
                    {
                        return await GenerateGuideAsync(
                            app.CandidateId,
                            new GenerateInterviewPrepRequestDto
                            {
                                CandidateId = app.CandidateId,
                                ApplicationId = app.Id,
                                JobId = app.JobId,
                                JobTitle = app.Job?.Title,
                                TargetRole = app.Job?.Title,
                                JobDescription = app.Job?.Description ?? string.Empty
                            },
                            cancellationToken);
                    }
                }
            }

            if (entity == null) return null;

            return MapEntityToDto(entity);
        }

        public async Task<InterviewPrepGuideDto?> GetLatestGuideAsync(
            Guid candidateId,
            Guid? jobId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.InterviewPrepGuides
                .AsNoTracking()
                .Where(g => g.CandidateId == candidateId || g.CandidateId == null);

            if (jobId.HasValue)
            {
                query = query.Where(g => g.JobId == jobId.Value);
            }

            var entity = await query
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null) return null;

            return await GetGuideByIdAsync(entity.Id, cancellationToken);
        }

        public async Task<List<InterviewPrepGuideDto>> GetCandidateGuidesAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default)
        {
            var guides = await _dbContext.InterviewPrepGuides
                .AsNoTracking()
                .Include(g => g.JobApplication)
                    .ThenInclude(a => a!.Job)
                        .ThenInclude(j => j!.Company)
                .Include(g => g.Job)
                    .ThenInclude(j => j!.Company)
                .Where(g => g.CandidateId == candidateId || g.CandidateId == null)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync(cancellationToken);

            var results = new List<InterviewPrepGuideDto>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entity in guides)
            {
                var dto = MapEntityToDto(entity);
                var key = $"{dto.CompanyName}-{dto.JobTitle}";
                if (seenKeys.Add(key))
                {
                    results.Add(dto);
                }
            }

            return results;
        }

        public async Task<InterviewPrepEligibilityDto> CheckEligibilityAsync(
            Guid candidateId,
            Guid? applicationId = null,
            Guid? jobId = null,
            CancellationToken cancellationToken = default)
        {
            JobApplication? application = null;

            if (applicationId.HasValue)
            {
                application = await _dbContext.JobApplications
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == applicationId.Value, cancellationToken);
            }
            else if (jobId.HasValue)
            {
                application = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Where(a => a.CandidateId == candidateId && a.JobId == jobId.Value)
                    .OrderByDescending(a => a.AppliedDate)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                application = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Where(a => a.CandidateId == candidateId)
                    .OrderByDescending(a => a.AppliedDate)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            CvEvaluationResult? cvEval = null;
            if (application != null)
            {
                cvEval = await _dbContext.CvEvaluationResults
                    .AsNoTracking()
                    .Where(r => r.CandidateId == candidateId && r.JobId == application.JobId)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (application == null)
            {
                return new InterviewPrepEligibilityDto
                {
                    IsEligible = true,
                    ApplicationStatus = "Interview",
                    Message = "Candidate is eligible for interview preparation."
                };
            }

            var currentStatus = application.Status ?? "Pending";
            var cvApproval = cvEval?.ApprovalStatus ?? "Pending";

            if (currentStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
                cvApproval.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return new InterviewPrepEligibilityDto
                {
                    IsEligible = false,
                    ApplicationStatus = "Rejected",
                    Message = "Access denied. Candidate is not eligible for interview preparation."
                };
            }

            bool isEligible = currentStatus.Equals("Interview", StringComparison.OrdinalIgnoreCase);

            if (!isEligible)
            {
                return new InterviewPrepEligibilityDto
                {
                    IsEligible = false,
                    ApplicationStatus = currentStatus,
                    Message = "Interview Preparation is strictly unlocked only when your application reaches the 'Interview' stage."
                };
            }

            return new InterviewPrepEligibilityDto
            {
                IsEligible = true,
                ApplicationStatus = currentStatus,
                Message = "Candidate is eligible for interview preparation."
            };
        }

        private static InterviewPrepGuideDto MapEntityToDto(InterviewPrepGuide entity)
        {
            var studyStore = DeserializeSafe<StudyGuidelineStore>(entity.TechnicalQuestionsJson);

            var theoreticalAreas = studyStore.KeyTheoreticalAreas ?? new List<StudyFocusAreaDto>();
            var coreConcepts = studyStore.TechnicalCoreConcepts ?? new List<StudyFocusAreaDto>();
            var practicalFocus = studyStore.PracticalImplementationFocus?.Count > 0 
                ? studyStore.PracticalImplementationFocus 
                : DeserializeSafe<List<StudyFocusAreaDto>>(entity.BehavioralQuestionsJson);

            var proTips = DeserializeSafe<List<string>>(entity.ProTipsJson);
            var checklist = DeserializeSafe<List<string>>(entity.ChecklistJson);

            var companyName = entity.JobApplication?.Job?.Company?.CompanyName
                ?? entity.Job?.Company?.CompanyName
                ?? "Enterprise Partner";

            var location = entity.JobApplication?.Job?.Location
                ?? entity.Job?.Location;

            var employmentType = entity.JobApplication?.Job?.EmploymentType
                ?? entity.Job?.EmploymentType;

            var appStatus = entity.JobApplication?.Status ?? "Interview";
            var appliedDate = entity.JobApplication?.AppliedDate;

            return new InterviewPrepGuideDto
            {
                Id = entity.Id,
                CandidateId = entity.CandidateId ?? Guid.Empty,
                ApplicationId = entity.ApplicationId,
                JobId = entity.JobId,
                JobTitle = entity.JobTitle,
                TargetRole = entity.TargetRole ?? entity.JobTitle,
                CompanyName = companyName,
                Location = location,
                EmploymentType = employmentType,
                ApplicationStatus = appStatus,
                AppliedDate = appliedDate,
                InterviewDate = appliedDate?.AddDays(7),
                JobDescription = entity.JobDescription,
                RoleOverviewSummary = entity.RoleOverviewSummary ?? string.Empty,
                KeyTheoreticalAreas = theoreticalAreas,
                TechnicalCoreConcepts = coreConcepts,
                PracticalImplementationFocus = practicalFocus,
                ProTips = proTips,
                PreparationChecklist = checklist,
                CreatedAt = entity.CreatedAt
            };
        }

        private static StudyFocusAreaDto MapFromPython(PythonStudyFocusArea p)
        {
            return new StudyFocusAreaDto
            {
                Id = !string.IsNullOrWhiteSpace(p.Id) ? p.Id : Guid.NewGuid().ToString(),
                Title = p.Title ?? string.Empty,
                Section = p.Section ?? "Key Theoretical Areas",
                Priority = p.Priority ?? "High Priority",
                EstimatedStudyTime = p.EstimatedStudyTime ?? "35-45 mins",
                Overview = p.Overview ?? string.Empty,
                ConceptsToReview = p.ConceptsToReview ?? new List<string>(),
                PracticalApplication = p.PracticalApplication ?? string.Empty,
                CoachTip = p.CoachTip ?? string.Empty
            };
        }

        private static T DeserializeSafe<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new T();
            }
            catch
            {
                return new T();
            }
        }
    }
}
