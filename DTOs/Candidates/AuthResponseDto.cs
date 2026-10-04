
namespace Skill_Hub_BackEnd.DTOs.Candidates
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
        public UserResponseDto User { get; set; } = null!;
    }
}
