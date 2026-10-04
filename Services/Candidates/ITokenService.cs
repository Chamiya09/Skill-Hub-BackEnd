
namespace Skill_Hub_BackEnd.Services.Candidates
{
    public interface ITokenService
    {
        (string token, DateTime expiresAt) GenerateToken(Company company);
        (string token, DateTime expiresAt) GenerateToken(User user, string? companyName);
    }
}
