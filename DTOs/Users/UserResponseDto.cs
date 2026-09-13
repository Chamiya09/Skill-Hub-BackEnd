namespace Skill_Hub_BackEnd.DTOs.Users
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public Guid? CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? AdminName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Headline { get; set; }
        public string? Phone { get; set; }
        public string? CompanySize { get; set; }
        public string? FoundedYear { get; set; }
        public string? LogoUrl { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Website { get; set; }
        public string? LinkedinUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? GithubUrl { get; set; }
        public string? Location { get; set; }
        public string? Industry { get; set; }
        public string? About { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
