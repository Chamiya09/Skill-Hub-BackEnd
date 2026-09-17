using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skill_Hub_BackEnd.Models
{
    [Table("Submissions", Schema = "public")]
    public class Submission
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid AssessmentId { get; set; }

        [ForeignKey(nameof(AssessmentId))]
        public Assessment? Assessment { get; set; }

        [Required]
        public Guid CandidateId { get; set; }

        [Required]
        public Guid ApplicationId { get; set; }

        [Required]
        public Guid JobVacancyId { get; set; }

        /// <summary>
        /// JSONB representation of the candidate's submitted code solutions and test results.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? Answers { get; set; }

        /// <summary>
        /// Technical exam score calculated from automated code evaluation (0-100).
        /// </summary>
        [Column(TypeName = "numeric(5,2)")]
        public decimal ExamScore { get; set; } = 0.00m;

        /// <summary>
        /// CV match score copied from Student 2's incoming shortlisted contract (0-100).
        /// </summary>
        [Column(TypeName = "numeric(5,2)")]
        public decimal CvScore { get; set; } = 0.00m;

        /// <summary>
        /// Final score for candidate ranking. Per requirements, this is based strictly on ExamScore.
        /// </summary>
        [Column(TypeName = "numeric(5,2)")]
        public decimal FinalWeightedScore { get; set; } = 0.00m;

        /// <summary>
        /// Assigned, Started, Submitted, Graded, Passed, Rejected
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Assigned";

        public DateTime? StartedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? GradedAt { get; set; }

        /// <summary>
        /// JSONB telemetry data capturing tab switches, blur counts, and timestamps.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? ProctorFlags { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

