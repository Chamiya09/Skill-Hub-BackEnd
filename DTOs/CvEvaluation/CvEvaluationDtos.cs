namespace Skill_Hub_BackEnd.DTOs.CvEvaluation
{
    // ─── Inbound DTO: POST /api/CVEvaluation/analyze ─────────────────────────────

    /// <summary>
    /// Request body that the React frontend sends to trigger the multi-agent evaluation pipeline.
    /// </summary>
    public sealed class AnalyzeCvRequestDto
    {
        /// <summary>The candidate's User ID whose CV will be evaluated.</summary>
        public Guid CandidateId { get; set; }

        /// <summary>The job vacancy ID to evaluate the CV against.</summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// The specific job application ID that links the candidate to this vacancy.
        /// Used to track the evaluation back to the application record.
        /// </summary>
        public Guid? ApplicationId { get; set; }

        /// <summary>
        /// When true, forces a fresh multi-agent run even if a cached result exists.
        /// Defaults to false for performance.
        /// </summary>
        public bool ForceRefresh { get; set; } = false;
    }

    // ─── Outbound DTO: response from POST /api/CVEvaluation/analyze ──────────────

    /// <summary>
    /// The evaluation result payload returned to the React frontend after the
    /// three-agent workflow completes: Extractor → Evaluator → Validator.
    /// </summary>
    public sealed class CvEvaluationResultDto
    {
        /// <summary>Unique ID of the persisted CvEvaluationResult record.</summary>
        public Guid Id { get; set; }

        public Guid CandidateId { get; set; }
        public Guid JobId { get; set; }

        // ─── AgentEvaluator.EvaluateMatch() output ─────────────────────────
        /// <summary>Overall match score 0–100.</summary>
        public int MatchScore { get; set; }

        /// <summary>Deserialized list of candidate strengths for direct UI binding.</summary>
        public List<string> Strengths { get; set; } = [];

        /// <summary>Deserialized list of missing/weak skills for direct UI binding.</summary>
        public List<string> MissingSkills { get; set; } = [];

        /// <summary>Free-text recommendation summary from AgentEvaluator.</summary>
        public string? Recommendation { get; set; }

        // ─── AgentValidator.ValidateBusinessRules() output ─────────────────
        /// <summary>Any validation warnings or adjustments applied to the score.</summary>
        public List<string> ValidationNotes { get; set; } = [];

        // ─── Human Approval State ────────────────────────────────────────────
        /// <summary>Current approval status: "Pending" | "Approved" | "Rejected".</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }
    }

    // ─── Inbound DTO: POST /api/CVEvaluation/{id}/approve ────────────────────────

    /// <summary>
    /// Request body for the human recruiter approval step.
    /// Finalises the HITL (Human-in-the-Loop) gate in the agentic workflow.
    /// </summary>
    public sealed class ApproveEvaluationRequestDto
    {
        /// <summary>
        /// Decision from the human reviewer.
        /// Accepted values: "Approved" | "Rejected"
        /// </summary>
        public string Decision { get; set; } = "Approved";

        /// <summary>Optional free-text notes from the recruiter.</summary>
        public string? ReviewerNotes { get; set; }
    }

    // ─── Outbound DTO: response from POST /api/CVEvaluation/{id}/approve ─────────

    /// <summary>
    /// Confirmation payload returned after the human approval action is saved.
    /// </summary>
    public sealed class ApproveEvaluationResponseDto
    {
        public Guid EvaluationId { get; set; }
        public Guid CandidateId { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public DateTime ApprovedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
