using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skill_Hub_BackEnd.Models
{
    [Table("CandidateEducations", Schema = "public")]
    public class CandidateEducation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        [MaxLength(200)]
        public string Degree { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Institution { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? FieldOfStudy { get; set; }

        [Required]
        [MaxLength(50)]
        public string StartYear { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? EndYear { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
