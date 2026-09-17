using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skill_Hub_BackEnd.Models
{
    public sealed class AiMatchResult
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CandidateId { get; set; }

        [ForeignKey(nameof(CandidateId))]
        public User? Candidate { get; set; }

        [Required]
        public Guid JobId { get; set; }

        [ForeignKey(nameof(JobId))]
        public JobVacancy? Job { get; set; }

        [Range(0, 100)]
        public int MatchPercentage { get; set; }

        public string? BreakdownJson { get; set; }
        public string? StrengthsJson { get; set; }
        public string? MissingSkillsJson { get; set; }
        public string? Recommendation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
