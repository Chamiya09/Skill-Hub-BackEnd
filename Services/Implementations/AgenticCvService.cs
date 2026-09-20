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
            var skillsTask        = _db.CandidateSkills.Where(s => s.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var experienceTask    = _db.CandidateExperiences.Where(e => e.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var educationTask     = _db.CandidateEducations.Where(e => e.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var projectsTask      = _db.CandidateProjects.Where(p => p.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);
            var certsTask         = _db.CandidateCertifications.Where(c => c.UserId == candidateId).AsNoTracking().ToListAsync(cancellationToken);

            await Task.WhenAll(skillsTask, experienceTask, educationTask, projectsTask, certsTask);

            // ── STEP 3: Load the job vacancy for requirement context ───────────────
            var job = await _db.JobVacancies
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
                ?? throw new KeyNotFoundException($"JobVacancy {jobId} not found.");

            _logger.LogInformation(
                "[AgentExtractor] Extracted {SkillCount} skills, {ExpCount} experience records for CandidateId={CandidateId}",
                skillsTask.Result.Count, experienceTask.Result.Count, candidateId);

            // ── STEP 4: Assemble structured payload ───────────────────────────────
            return new ExtractedCandidateData
            {
                CandidateName     = candidate.FullName ?? $"{candidate.FirstName} {candidate.LastName}".Trim(),
                Headline          = candidate.Headline ?? string.Empty,
                Location          = candidate.Location ?? string.Empty,
                Skills            = skillsTask.Result.Select(s => s.SkillName).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
                ExperienceSummary = experienceTask.Result
                    .Select(e => $"{e.Title} at {e.Company} ({e.StartDate} – {(e.IsCurrent ? "Present" : e.EndDate ?? "N/A")}): {e.Description ?? string.Empty}")
                    .ToList(),
                EducationSummary  = educationTask.Result
                    .Select(e => $"{e.Degree} in {e.FieldOfStudy ?? "General"} from {e.Institution} ({e.StartYear} – {e.EndYear ?? "Present"})")
                    .ToList(),
                ProjectSummary    = projectsTask.Result
                    .Select(p => $"{p.ProjectName} [{p.Role ?? "Contributor"}]: {p.Description ?? string.Empty}")
                    .ToList(),
                Certifications    = certsTask.Result.Select(c => $"{c.Title} ({c.IssuingOrganization})").Where(c => !string.IsNullOrWhiteSpace(c)).ToList(),
                JobTitle          = job.Title,
                // JobVacancy has no RequiredSkills field — derive from Description + WhatWeOffer
                JobDescription    = job.Description,
                RequiredSkills    = job.WhatWeOffer ?? job.Description,
            };
        }
    }

    /// <summary>
    /// Agent 2 – Evaluator
    /// Responsibility: Use an LLM (Groq / LangGraph) to compare the extracted candidate
    /// data against the job requirements and produce a structured evaluation result.
    /// </summary>
    file sealed class AgentEvaluator
    {
        private readonly IAiAgentService _aiService;
        private readonly ILogger<AgentEvaluator> _logger;

        public AgentEvaluator(IAiAgentService aiService, ILogger<AgentEvaluator> logger)
        {
            _aiService = aiService;
            _logger    = logger;
        }

        /// <summary>
        /// Calls the AI model with the extracted candidate data and job requirements.
        /// Returns a structured evaluation containing score, strengths, missing skills,
        /// and a qualitative recommendation.
        /// </summary>
        public async Task<EvaluationOutput> EvaluateMatchAsync(
            ExtractedCandidateData candidateData,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[AgentEvaluator] Starting LLM evaluation for candidate '{Name}' against '{JobTitle}'",
                candidateData.CandidateName, candidateData.JobTitle);

            // ── BUILD STRUCTURED PROMPT ────────────────────────────────────────────
            //  TODO: Replace with your production prompt template (RAG / fine-tuned).
            var systemPrompt = @"
You are an expert senior HR analyst and technical recruiter with 15 years of experience.
Your task is to evaluate a candidate's CV against a specific job vacancy and provide a structured JSON assessment.

Respond ONLY with a valid JSON object in this exact format (no markdown, no prose):
{
  ""matchScore"": <integer 0-100>,
  ""strengths"": [""<strength 1>"", ""<strength 2>"", ...],
  ""missingSkills"": [""<gap 1>"", ""<gap 2>"", ...],
  ""recommendation"": ""<2-3 sentence qualitative summary>""
}

Scoring guide:
- 90-100: Exceptional fit, exceeds all requirements
- 75-89:  Strong fit, meets most requirements with minor gaps
- 60-74:  Moderate fit, meets core requirements but has notable gaps
- 40-59:  Partial fit, significant skill or experience gaps
- 0-39:   Poor fit, does not meet minimum requirements";

            var userPrompt = $@"
=== JOB VACANCY ===
Title: {candidateData.JobTitle}
Required Skills: {candidateData.RequiredSkills}
Job Description: {candidateData.JobDescription}

=== CANDIDATE PROFILE ===
Name: {candidateData.CandidateName}
Headline: {candidateData.Headline}
Location: {candidateData.Location}

Skills: {string.Join(", ", candidateData.Skills)}

Experience:
{string.Join("\n", candidateData.ExperienceSummary.Select(e => $"  • {e}"))}

Education:
{string.Join("\n", candidateData.EducationSummary.Select(e => $"  • {e}"))}

Projects:
{string.Join("\n", candidateData.ProjectSummary.Select(p => $"  • {p}"))}

Certifications: {string.Join(", ", candidateData.Certifications)}

Please evaluate this candidate against the job vacancy and provide your structured JSON assessment.";

            // ── AI CALL (placeholder – wire to IAiAgentService or LangGraph) ───────
            // TODO: Replace with actual AI service call. The IAiAgentService interface
            //       already exists in the project at Services/Interfaces/IAiAgentService.cs.
            //       Use: var rawJson = await _aiService.AnalyzeAsync(systemPrompt, userPrompt, cancellationToken);
            //
            //       For now, a mock result is returned so the endpoint is functional during development.
            var rawJson = GenerateMockEvaluation(candidateData);

            _logger.LogInformation("[AgentEvaluator] LLM returned evaluation for '{Name}'", candidateData.CandidateName);

            // ── PARSE AI RESPONSE ─────────────────────────────────────────────────
            try
            {
                var parsed = JsonSerializer.Deserialize<EvaluationOutput>(rawJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                }) ?? throw new InvalidOperationException("AI returned empty or unparseable response.");

                return parsed;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[AgentEvaluator] Failed to parse AI response. Raw: {Raw}", rawJson);
                throw new InvalidOperationException("AI evaluation response was not valid JSON. Check the prompt and model configuration.", ex);
            }
        }

        // ── MOCK EVALUATOR (remove when real AI is wired) ─────────────────────────
        private static string GenerateMockEvaluation(ExtractedCandidateData data)
        {
            var rng   = new Random();
            var score = rng.Next(62, 93);

            var matchedSkills  = data.Skills.Take(3).ToList();
            var requiredSkills = (data.RequiredSkills ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var missing        = requiredSkills.Except(data.Skills, StringComparer.OrdinalIgnoreCase).Take(3).ToList();

            if (missing.Count == 0) missing = ["Advanced system design", "Production Kubernetes ops"];

            return JsonSerializer.Serialize(new
            {
                matchScore     = score,
                strengths      = matchedSkills.Count > 0 ? matchedSkills : new List<string> { "Relevant technical background", "Strong project portfolio" },
                missingSkills  = missing,
                recommendation = $"Candidate shows a {score}% alignment with the {data.JobTitle} role. " +
                                 $"Demonstrated strengths in {string.Join(" and ", matchedSkills.Take(2))}. " +
                                 $"Recommend scheduling a technical interview to assess depth in {missing.FirstOrDefault() ?? "core requirements"}.",
            });
        }
    }

    /// <summary>
    /// Agent 3 – Validator
    /// Responsibility: Apply deterministic business rules on top of the AI-generated score.
    /// This is the policy/guardrail layer that ensures scores adhere to company hiring rules.
    /// </summary>
    file sealed class AgentValidator
    {
        private readonly ILogger<AgentValidator> _logger;

        public AgentValidator(ILogger<AgentValidator> logger) => _logger = logger;

        /// <summary>
        /// Validates and adjusts the AI evaluation output according to deterministic business rules.
        /// Appends human-readable notes for any adjustments made.
        /// </summary>
        public ValidationResult ValidateBusinessRules(
            EvaluationOutput evaluation,
            ExtractedCandidateData candidateData)
        {
            _logger.LogInformation(
                "[AgentValidator] Validating business rules. Raw score: {Score}", evaluation.MatchScore);

            var notes       = new List<string>();
            var finalScore  = evaluation.MatchScore;
            var finalStrengths     = evaluation.Strengths ?? [];
            var finalMissingSkills = evaluation.MissingSkills ?? [];

            // ── RULE 1: Score ceiling ─────────────────────────────────────────────
            if (finalScore > 100) { finalScore = 100; notes.Add("Score capped at 100 (AI over-reported)."); }
            if (finalScore < 0)   { finalScore = 0;   notes.Add("Score floored at 0 (AI under-reported).");  }

            // ── RULE 2: Mandatory experience gate ─────────────────────────────────
            //  If candidate has ZERO experience records, cap score at 75 to prevent false positives.
            if (candidateData.ExperienceSummary.Count == 0 && finalScore > 75)
            {
                finalScore = 75;
                notes.Add("Score capped at 75: no professional experience records on profile.");
            }

            // ── RULE 3: Skills coverage minimum ──────────────────────────────────
            if (candidateData.Skills.Count < 3)
            {
                notes.Add("Candidate profile has fewer than 3 skills listed — recommend requesting an updated CV.");
            }

            // ── RULE 4: Missing critical skills penalty ───────────────────────────
            //  If there are 5+ missing skills, apply a 10-point deduction capped at 50.
            if (finalMissingSkills.Count >= 5 && finalScore > 50)
            {
                finalScore -= 10;
                notes.Add($"Score adjusted –10 points: {finalMissingSkills.Count} significant skill gaps identified.");
            }

            // ── RULE 5: Score band normalisation ─────────────────────────────────
            //  Scores 1–9 are non-standard — round up to 10 to avoid misleading displays.
            if (finalScore is > 0 and < 10)
            {
                finalScore = 10;
                notes.Add("Score normalised to 10 (minimum display threshold).");
            }

            _logger.LogInformation(
                "[AgentValidator] Validation complete. Final score: {Score}, Notes: {Count}",
                finalScore, notes.Count);

            return new ValidationResult
            {
                FinalScore      = finalScore,
                Strengths       = finalStrengths,
                MissingSkills   = finalMissingSkills,
                Recommendation  = evaluation.Recommendation ?? string.Empty,
                ValidationNotes = notes,
            };
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

    file sealed class EvaluationOutput
    {
        public int MatchScore              { get; set; }
        public List<string> Strengths      { get; set; } = [];
        public List<string> MissingSkills  { get; set; } = [];
        public string? Recommendation      { get; set; }
    }

    file sealed class ValidationResult
    {
        public int FinalScore              { get; init; }
        public List<string> Strengths      { get; init; } = [];
        public List<string> MissingSkills  { get; init; } = [];
        public string Recommendation       { get; init; } = string.Empty;
        public List<string> ValidationNotes { get; init; } = [];
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
        private readonly IAiAgentService        _aiService;
        private readonly ILogger<AgenticCvService> _logger;

        public AgenticCvService(
            ApplicationDbContext   db,
            IAiAgentService        aiService,
            ILogger<AgenticCvService> logger)
        {
            _db        = db;
            _aiService = aiService;
            _logger    = logger;
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
            // AGENT 2 — EVALUATOR: AI-powered match scoring
            // ══════════════════════════════════════════════════════════════════════
            var evaluatorLogger  = _logger as ILogger<AgentEvaluator> ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AgentEvaluator>();
            var evaluator        = new AgentEvaluator(_aiService, evaluatorLogger);
            var evaluationOutput = await evaluator.EvaluateMatchAsync(extractedData, cancellationToken);

            // ══════════════════════════════════════════════════════════════════════
            // AGENT 3 — VALIDATOR: Apply deterministic business rules + guardrails
            // ══════════════════════════════════════════════════════════════════════
            var validatorLogger  = _logger as ILogger<AgentValidator> ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AgentValidator>();
            var validator        = new AgentValidator(validatorLogger);
            var validationResult = validator.ValidateBusinessRules(evaluationOutput, extractedData);

            // ── PERSIST to PostgreSQL ─────────────────────────────────────────────
            var entity = new CvEvaluationResult
            {
                CandidateId        = request.CandidateId,
                JobId              = request.JobId,
                ApplicationId      = request.ApplicationId,
                MatchScore         = validationResult.FinalScore,
                StrengthsJson      = JsonSerializer.Serialize(validationResult.Strengths),
                MissingSkillsJson  = JsonSerializer.Serialize(validationResult.MissingSkills),
                Recommendation     = validationResult.Recommendation,
                ExtractedDataJson  = JsonSerializer.Serialize(extractedData),
                ValidationNotesJson = JsonSerializer.Serialize(validationResult.ValidationNotes),
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
