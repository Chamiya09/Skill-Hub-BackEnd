using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skill_Hub_BackEnd.Models
{
    [Table("Assessments", Schema = "public")]
    public class Assessment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Reference to JobVacancy. No strict cross-module DB constraint per architecture isolation rules.
        /// </summary>
        [Required]
        public Guid JobVacancyId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Raw AI-generated question bank stored as JSONB before HR reviews/edits.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string GeneratedQuestions { get; set; } = "[]";

        /// <summary>
        /// HR-approved/edited question set actually published for candidate exams.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string FinalQuestions { get; set; } = "[]";

        [Column(TypeName = "numeric(5,2)")]
        public decimal PassingThreshold { get; set; } = 60.00m;

        public int TimeLimitMinutes { get; set; } = 60;

        /// <summary>
        /// ID of the HR manager / recruiter who created this assessment.
        /// </summary>
        [Required]
        public Guid CreatedBy { get; set; }

        /// <summary>
        /// Draft, Published, Archived
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}

