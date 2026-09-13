using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.Models
{
    public class Company
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? AdminName { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string ContactEmail { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? CompanySize { get; set; }

        [MaxLength(50)]
        public string? FoundedYear { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(255)]
        public string? Website { get; set; }

        [MaxLength(255)]
        public string? LinkedinUrl { get; set; }

        [MaxLength(255)]
        public string? TwitterUrl { get; set; }

        [MaxLength(255)]
        public string? GithubUrl { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        [MaxLength(100)]
        public string? Industry { get; set; }

        public string? About { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for company users if provisioned in the future
        public ICollection<User> Users { get; set; } = new List<User>();

        // Navigation property for Job Vacancies posted by this Company
        public ICollection<JobVacancy> JobVacancies { get; set; } = new List<JobVacancy>();
    }
}
