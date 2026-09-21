using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Skill_Hub_BackEnd.Models;

namespace Skill_Hub_BackEnd.Models
{
    /// <summary>
    /// Represents the persisted output of the agentic Multi-Agent CV Evaluation workflow.
    /// Shared state object passed between AgentExtractor → AgentEvaluator → AgentValidator.
    /// </summary>
    public sealed class CvEvaluationResult
    {
        // ─── Primary Key ─────────────────────────────────────────────────────────
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // ─── Candidate & Job References ───────────────────────────────────────────
        /// <summary>Foreign key to Users (candidate).</summary>
        [Required]
        public Guid CandidateId { get; set; }

        [ForeignKey(nameof(CandidateId))]
        public User? Candidate { get; set; }

        /// <summary>Foreign key to the job vacancy that was evaluated against.</summary>
        [Required]
        public Guid JobId { get; set; }

        [ForeignKey(nameof(JobId))]
        public JobVacancy? Job { get; set; }

        /// <summary>Foreign key to the specific job application that triggered the evaluation.</summary>
        public Guid? ApplicationId { get; set; }

        // ─── Agent Output: Scoring ────────────────────────────────────────────────
        /// <summary>
        /// Overall match score (0–100) produced by AgentEvaluator.EvaluateMatch().
        /// Validated by AgentValidator.ValidateBusinessRules() before persisting.
        /// </summary>
        [Range(0, 100)]
        public int MatchScore { get; set; }

        // ─── Agent Output: Qualitative Insights (stored as JSON arrays) ───────────
        /// <summary>
        /// Serialised JSON array of candidate strength strings extracted by AgentEvaluator.
        /// Example: ["5+ years TypeScript", "Led cross-functional teams", "AWS Certified"]
        /// </summary>
        public string? StrengthsJson { get; set; }

        /// <summary>
        /// Serialised JSON array of missing or weak skills identified by AgentEvaluator.
        /// Example: ["Kubernetes", "GraphQL", "System Design experience"]
        /// </summary>
        public string? MissingSkillsJson { get; set; }

        /// <summary>
        /// Free-text overall recommendation summary produced by AgentEvaluator.
        /// </summary>
        public string? Recommendation { get; set; }

        /// <summary>
        /// Raw structured data extracted by AgentExtractor.ExtractData() stored as JSON.
        /// Contains parsed CV sections: experience, education, skills, projects, certifications.
        /// </summary>
        public string? ExtractedDataJson { get; set; }

        /// <summary>
        /// Validation flags or warnings appended by AgentValidator.ValidateBusinessRules().
        /// Example: ["Score capped at 95 – mandatory skill missing", "Location mismatch warning"]
        /// </summary>
        public string? ValidationNotesJson { get; set; }

        // ─── Human-in-the-Loop Approval ───────────────────────────────────────────
        /// <summary>
        /// Human approval status set via the POST /{id}/approve endpoint.
        /// Values: "Pending" | "Approved" | "Rejected"
        /// </summary>
        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "Pending";

        /// <summary>Optional reviewer notes added during the human approval step.</summary>
        public string? ReviewerNotes { get; set; }

        /// <summary>UserId of the company recruiter who performed the approval action.</summary>
        public Guid? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // ─── Audit Fields ─────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
