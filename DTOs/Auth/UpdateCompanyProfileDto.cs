using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Auth
{
    public class UpdateCompanyProfileDto
    {
        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [MaxLength(100)]
        public string? Industry { get; set; }

        [MaxLength(255)]
        public string? Website { get; set; }
    }
}
