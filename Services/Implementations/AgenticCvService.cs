using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.CvEvaluation;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    // ═══════════════════════════════════════════════════════════════════════════════
    //  PLACEHOLDER AGENT CLASSES
    //  Each of these represents one autonomous agent in the evaluation pipeline.
    //  Replace the placeholder comments with real AI SDK / LangGraph calls.
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Agent 1 – Extractor
    /// Responsibility: Hydrate raw candidate profile data from the database into a
    /// normalised, structured format that the downstream agents can reason over.
    /// </summary>
    file sealed class AgentExtractor
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AgentExtractor> _logger;

        public AgentExtractor(ApplicationDbContext db, ILogger<AgentExtractor> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Fetches and assembles the candidate's complete profile from all related tables.
        /// Returns a structured JSON string that is passed to <see cref="AgentEvaluator"/>.
        /// </summary>
        public async Task<ExtractedCandidateData> ExtractDataAsync(
            Guid candidateId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[AgentExtractor] Extracting profile data for CandidateId={CandidateId} against JobId={JobId}",
                candidateId, jobId);

            // ── STEP 1: Load candidate base profile ────────────────────────────────
            var candidate = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == candidateId, cancellationToken)
                ?? throw new KeyNotFoundException($"Candidate {candidateId} not found.");

            // ── STEP 2: Load structured CV data (parallel queries for performance) ─
            var skills        = await _db.CandidateSkills.Where(s => s.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var experience    = await _db.CandidateExperiences.Where(e => e.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var education     = await _db.CandidateEducations.Where(e => e.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var projects      = await _db.CandidateProjects.Where(p => p.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var certs         = await _db.CandidateCertifications.Where(c => c.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);

            // ── STEP 3: Load the job vacancy for requirement context ───────────────
            var job = await _db.JobVacancies
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
                ?? throw new KeyNotFoundException($"JobVacancy {jobId} not found.");

            _logger.LogInformation(
                "[AgentExtractor] Extracted {SkillCount} skills, {ExpCount} experience records for CandidateId={CandidateId}",
                skills.Count, experience.Count, candidateId);

            // ── STEP 4: Assemble structured payload ───────────────────────────────
            return new ExtractedCandidateData
            {
                CandidateName     = candidate.FullName ?? $"{candidate.FirstName} {candidate.LastName}".Trim(),
                Headline          = candidate.Headline ?? string.Empty,
                Location          = candidate.Location ?? string.Empty,
                Skills            = skills.Select(s => s.SkillName).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
                ExperienceSummary = experience
                    .Select(e => $"{e.Title} at {e.Company} ({e.StartDate} – {(e.IsCurrent ? "Present" : e.EndDate ?? "N/A")}): {e.Description ?? string.Empty}")
                    .ToList(),
                EducationSummary  = education
                    .Select(e => $"{e.Degree} in {e.FieldOfStudy ?? "General"} from {e.Institution} ({e.StartYear} – {e.EndYear ?? "Present"})")
                    .ToList(),
                ProjectSummary    = projects
                    .Select(p => $"{p.ProjectName} [{p.Role ?? "Contributor"}]: {p.Description ?? string.Empty}")
                    .ToList(),
                Certifications    = certs.Select(c => $"{c.Title} ({c.IssuingOrganization})").Where(c => !string.IsNullOrWhiteSpace(c)).ToList(),
                JobTitle          = job.Title,
                // JobVacancy has no RequiredSkills field — derive from Description + WhatWeOffer
                JobDescription    = job.Description,
                RequiredSkills    = job.WhatWeOffer ?? job.Description,
            };
        }
    }

    /// <summary>
    /// AI Pipeline Client Adapter
    /// Formats the extracted candidate data into raw text and forwards it
    /// to the Python microservice which executes the 3-Agent pipeline.
    /// </summary>
    file sealed class PythonPipelineAdapter
    {
        private readonly IPythonCvEvalClient _pythonClient;
        private readonly ILogger _logger;

        public PythonPipelineAdapter(IPythonCvEvalClient pythonClient, ILogger logger)
        {
            _pythonClient = pythonClient;
            _logger = logger;
        }

        public async Task<PythonCvEvalResponse> RunRemotePipelineAsync(
            ExtractedCandidateData data,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[PythonPipelineAdapter] Forwarding candidate '{Name}' to Python 3-Agent pipeline.",
                data.CandidateName);

            // Format the structured DB data into a single text blob for the Python Agent 1 to parse.
            var cvText = $@"
Name: {data.CandidateName}
Headline: {data.Headline}
Location: {data.Location}

Skills: {string.Join(", ", data.Skills)}

Experience:
{string.Join("\n", data.ExperienceSummary.Select(e => $"  • {e}"))}

Education:
{string.Join("\n", data.EducationSummary.Select(e => $"  • {e}"))}

Projects:
{string.Join("\n", data.ProjectSummary.Select(p => $"  • {p}"))}

Certifications: {string.Join(", ", data.Certifications)}";

            var jobDescription = $@"
Title: {data.JobTitle}
Required Skills: {data.RequiredSkills}
Job Description: {data.JobDescription}";

            return await _pythonClient.EvaluateAsync(cvText, jobDescription, data.RequiredSkills, cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    //  INTERNAL DATA TRANSFER RECORDS (pipeline-only, not exposed to API)
    // ═══════════════════════════════════════════════════════════════════════════════

    file sealed class ExtractedCandidateData
    {
        public string CandidateName     { get; init; } = string.Empty;
        public string Headline          { get; init; } = string.Empty;
        public string Location          { get; init; } = string.Empty;
        public List<string> Skills      { get; init; } = [];
        public List<string> ExperienceSummary { get; init; } = [];
        public List<string> EducationSummary  { get; init; } = [];
        public List<string> ProjectSummary    { get; init; } = [];
        public List<string> Certifications    { get; init; } = [];
        public string JobTitle          { get; init; } = string.Empty;
        public string JobDescription    { get; init; } = string.Empty;
        public string RequiredSkills    { get; init; } = string.Empty;
    }



    // ═══════════════════════════════════════════════════════════════════════════════
    //  AGENTIC CV SERVICE – ORCHESTRATOR
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orchestrates the three-agent CV evaluation pipeline and persists results
    /// to the CvEvaluationResults table in PostgreSQL via EF Core.
    ///
    /// Registered in DI as: builder.Services.AddScoped&lt;IAgenticCvService, AgenticCvService&gt;();
    /// </summary>
    public sealed class AgenticCvService : IAgenticCvService
    {
        private readonly ApplicationDbContext   _db;
        private readonly IPythonCvEvalClient    _pythonClient;
        private readonly ILogger<AgenticCvService> _logger;

        public AgenticCvService(
            ApplicationDbContext   db,
            IPythonCvEvalClient    pythonClient,
            ILogger<AgenticCvService> logger)
        {
            _db           = db;
            _pythonClient = pythonClient;
            _logger       = logger;
        }

        // ── PUBLIC: AnalyzeCvAsync ────────────────────────────────────────────────

        /// <inheritdoc />
        public async Task<CvEvaluationResultDto> AnalyzeCvAsync(
            AnalyzeCvRequestDto request,
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[AgenticCvService] AnalyzeCvAsync started — CandidateId={CandidateId}, JobId={JobId}, ForceRefresh={ForceRefresh}",
                request.CandidateId, request.JobId, request.ForceRefresh);

            // ── CHECK CACHE: Skip agents if a fresh result already exists ──────────
            if (!request.ForceRefresh)
            {
                var cached = await _db.Set<CvEvaluationResult>()
                    .Where(r => r.CandidateId == request.CandidateId && r.JobId == request.JobId)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (cached is not null && cached.CreatedAt > DateTime.UtcNow.AddHours(-24))
                {
                    _logger.LogInformation("[AgenticCvService] Returning cached evaluation {Id}", cached.Id);
                    return MapToDto(cached);
                }
            }

            // ══════════════════════════════════════════════════════════════════════
            // AGENT 1 — EXTRACTOR: Pull and normalise candidate profile data
            // ══════════════════════════════════════════════════════════════════════
            var extractorLogger  = _logger as ILogger<AgentExtractor> ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AgentExtractor>();
            var extractor        = new AgentExtractor(_db, extractorLogger);
            var extractedData    = await extractor.ExtractDataAsync(request.CandidateId, request.JobId, cancellationToken);

            // ══════════════════════════════════════════════════════════════════════
            // PYTHON MICROSERVICE CALL
            // ══════════════════════════════════════════════════════════════════════
            // The Python service handles Agent 1 (Extract), Agent 2 (Evaluate), and Agent 3 (Validate)
            var adapterLogger = _logger as ILogger<PythonPipelineAdapter> ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PythonPipelineAdapter>();
            var adapter       = new PythonPipelineAdapter(_pythonClient, adapterLogger);
            var result        = await adapter.RunRemotePipelineAsync(extractedData, cancellationToken);

            // ── PERSIST to PostgreSQL ─────────────────────────────────────────────
            var entity = new CvEvaluationResult
            {
                CandidateId        = request.CandidateId,
                JobId              = request.JobId,
                ApplicationId      = request.ApplicationId,
                MatchScore         = result.MatchScore,
                StrengthsJson      = JsonSerializer.Serialize(result.Strengths),
                MissingSkillsJson  = JsonSerializer.Serialize(result.MissingSkills),
                Recommendation     = result.Recommendation,
                ExtractedDataJson  = JsonSerializer.Serialize(extractedData),
                ValidationNotesJson = JsonSerializer.Serialize(result.ValidationNotes),
                ApprovalStatus     = "Pending",
            };

            _db.Set<CvEvaluationResult>().Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[AgenticCvService] Evaluation persisted — Id={Id}, Score={Score}",
                entity.Id, entity.MatchScore);

            return MapToDto(entity);
        }

        // ── PUBLIC: ApproveEvaluationAsync ───────────────────────────────────────

        /// <inheritdoc />
        public async Task<ApproveEvaluationResponseDto> ApproveEvaluationAsync(
            Guid evaluationId,
            ApproveEvaluationRequestDto request,
            Guid approvingUserId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[AgenticCvService] ApproveEvaluationAsync — EvaluationId={Id}, Decision={Decision}",
                evaluationId, request.Decision);

            var evaluation = await _db.Set<CvEvaluationResult>()
                .FirstOrDefaultAsync(r => r.Id == evaluationId, cancellationToken)
                ?? throw new KeyNotFoundException($"CvEvaluationResult {evaluationId} not found.");

            // ── Validate decision value ───────────────────────────────────────────
            if (request.Decision is not ("Approved" or "Rejected"))
                throw new ArgumentException($"Invalid decision '{request.Decision}'. Must be 'Approved' or 'Rejected'.");

            evaluation.ApprovalStatus    = request.Decision;
            evaluation.ReviewerNotes     = request.ReviewerNotes;
            evaluation.ApprovedByUserId  = approvingUserId;
            evaluation.ApprovedAt        = DateTime.UtcNow;
            evaluation.UpdatedAt         = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[AgenticCvService] Evaluation {Id} marked as {Decision}", evaluationId, request.Decision);

            return new ApproveEvaluationResponseDto
            {
                EvaluationId   = evaluation.Id,
                CandidateId    = evaluation.CandidateId,
                ApprovalStatus = evaluation.ApprovalStatus,
                ApprovedAt     = evaluation.ApprovedAt!.Value,
                Message        = $"Candidate has been successfully {request.Decision.ToLower()} and shortlisted.",
            };
        }

        // ── PUBLIC: GetEvaluationByIdAsync ───────────────────────────────────────

        /// <inheritdoc />
        public async Task<CvEvaluationResultDto?> GetEvaluationByIdAsync(
            Guid evaluationId,
            CancellationToken cancellationToken = default)
        {
            var result = await _db.Set<CvEvaluationResult>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == evaluationId, cancellationToken);

            return result is null ? null : MapToDto(result);
        }

        // ── PUBLIC: GetLatestEvaluationAsync ─────────────────────────────────────

        /// <inheritdoc />
        public async Task<CvEvaluationResultDto?> GetLatestEvaluationAsync(
            Guid candidateId,
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            var result = await _db.Set<CvEvaluationResult>()
                .AsNoTracking()
                .Where(r => r.CandidateId == candidateId && r.JobId == jobId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? null : MapToDto(result);
        }

        // ── PRIVATE: Entity → DTO mapper ─────────────────────────────────────────

        private static CvEvaluationResultDto MapToDto(CvEvaluationResult entity)
        {
            static List<string> DeserializeList(string? json)
            {
                if (string.IsNullOrWhiteSpace(json)) return [];
                try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
                catch { return []; }
            }

            return new CvEvaluationResultDto
            {
                Id              = entity.Id,
                CandidateId     = entity.CandidateId,
                JobId           = entity.JobId,
                MatchScore      = entity.MatchScore,
                Strengths       = DeserializeList(entity.StrengthsJson),
                MissingSkills   = DeserializeList(entity.MissingSkillsJson),
                Recommendation  = entity.Recommendation,
                ValidationNotes = DeserializeList(entity.ValidationNotesJson),
                ApprovalStatus  = entity.ApprovalStatus,
                CreatedAt       = entity.CreatedAt,
            };
        }
    }
}
