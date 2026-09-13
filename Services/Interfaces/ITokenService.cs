using Skill_Hub_BackEnd.Models;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface ITokenService
    {
        (string token, DateTime expiresAt) GenerateToken(Company company);
        (string token, DateTime expiresAt) GenerateToken(User user, string? companyName);
    }
}
