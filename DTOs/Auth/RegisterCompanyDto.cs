using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Auth
{
    public class RegisterCompanyDto
    {
        [Required(ErrorMessage = "Company name is required.")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Company name must be between 2 and 200 characters.")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Company business email is required.")]
        [EmailAddress(ErrorMessage = "Invalid business email address.")]
        public string CompanyEmail { get; set; } = string.Empty;

        public string? Industry { get; set; }

        public string? Website { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; } = string.Empty;
    }
}
