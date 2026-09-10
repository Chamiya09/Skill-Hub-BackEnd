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

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string ContactEmail { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Industry { get; set; }

        [MaxLength(255)]
        public string? Website { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for company users if provisioned in the future
        public ICollection<User> Users { get; set; } = new List<User>();

        // Navigation property for Job Vacancies posted by this Company
        public ICollection<JobVacancy> JobVacancies { get; set; } = new List<JobVacancy>();
    }
}
