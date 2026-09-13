using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Auth
{
    public class UpdateCompanyProfileDto
    {
        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [MaxLength(150)]
        public string? AdminName { get; set; }

        [EmailAddress]
        [MaxLength(255)]
        public string? ContactEmail { get; set; }

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
    }
}
