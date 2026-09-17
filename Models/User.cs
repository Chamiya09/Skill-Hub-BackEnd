using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Skill_Hub_BackEnd.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public Company? Company { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "CANDIDATE"; // e.g. CANDIDATE, HR_Admin, Recruiter, Hiring_Manager

        [MaxLength(200)]
        public string? Headline { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        [MaxLength(100)]
        public string? Experience { get; set; }

        [MaxLength(100)]
        public string? Availability { get; set; }

        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        public string? About { get; set; }

        public string? KeyHighlights { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CandidateSkill> Skills { get; set; } = new List<CandidateSkill>();
        public ICollection<CandidateExperience> Experiences { get; set; } = new List<CandidateExperience>();
        public ICollection<CandidateCertification> Certifications { get; set; } = new List<CandidateCertification>();
        public ICollection<CandidateProject> Projects { get; set; } = new List<CandidateProject>();
        public ICollection<CandidateEducation> Educations { get; set; } = new List<CandidateEducation>();
    }
}
