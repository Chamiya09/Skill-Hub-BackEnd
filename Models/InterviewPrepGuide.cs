using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skill_Hub_BackEnd.Models
{
    /// <summary>
    /// Represents an AI-generated Interview Preparation Guide tailored for a specific candidate
    /// and target job vacancy or custom job description (Student 1 module).
    /// </summary>
    public sealed class InterviewPrepGuide
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Optional foreign key to the candidate (User).</summary>
        public Guid? CandidateId { get; set; }

        [ForeignKey(nameof(CandidateId))]
        public User? Candidate { get; set; }

        /// <summary>Optional foreign key to the specific JobApplication.</summary>
        public Guid? ApplicationId { get; set; }

        [ForeignKey(nameof(ApplicationId))]
        public JobApplication? JobApplication { get; set; }

        /// <summary>Optional foreign key to an applied JobVacancy on the platform.</summary>
        public Guid? JobId { get; set; }

        [ForeignKey(nameof(JobId))]
        public JobVacancy? Job { get; set; }

        [Required]
        [MaxLength(200)]
        public string JobTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TargetRole { get; set; }

        [Required]
        public string JobDescription { get; set; } = string.Empty;

        /// <summary>Serialised JSON array of technical questions and suggested answers.</summary>
        [Column(TypeName = "jsonb")]
        public string TechnicalQuestionsJson { get; set; } = "[]";

        /// <summary>Serialised JSON array of behavioral questions (STAR framework).</summary>
        [Column(TypeName = "jsonb")]
        public string BehavioralQuestionsJson { get; set; } = "[]";

        /// <summary>Serialised JSON array of pro tips & preparation strategies.</summary>
        [Column(TypeName = "jsonb")]
        public string ProTipsJson { get; set; } = "[]";

        /// <summary>Serialised JSON array of preparation checklist items.</summary>
        [Column(TypeName = "jsonb")]
        public string ChecklistJson { get; set; } = "[]";

        public string? RoleOverviewSummary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
