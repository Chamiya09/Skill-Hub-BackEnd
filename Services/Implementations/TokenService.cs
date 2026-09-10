using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public (string token, DateTime expiresAt) GenerateToken(Company company)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] 
                ?? "SkillHub_Super_Secret_Production_Security_Key_2026_ATS_AI_Matching_Engine_Secure!";
            var issuer = jwtSettings["Issuer"] ?? "SkillHubApi";
            var audience = jwtSettings["Audience"] ?? "SkillHubClients";
            var expirationHours = int.TryParse(jwtSettings["ExpirationInHours"], out var hours) ? hours : 24;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, company.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, company.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, company.ContactEmail),
                new Claim(ClaimTypes.Email, company.ContactEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("name", company.CompanyName),
                new Claim(ClaimTypes.Name, company.CompanyName),
                new Claim("role", "Company"),
                new Claim(ClaimTypes.Role, "Company"),
                new Claim("companyId", company.Id.ToString()),
                new Claim("companyName", company.CompanyName)
            };

            var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(securityToken);

            return (tokenString, expiresAt);
        }

        public (string token, DateTime expiresAt) GenerateToken(User user, string? companyName)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] 
                ?? "SkillHub_Super_Secret_Production_Security_Key_2026_ATS_AI_Matching_Engine_Secure!";
            var issuer = jwtSettings["Issuer"] ?? "SkillHubApi";
            var audience = jwtSettings["Audience"] ?? "SkillHubClients";
            var expirationHours = int.TryParse(jwtSettings["ExpirationInHours"], out var hours) ? hours : 24;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("name", user.FullName),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim("role", user.Role),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("companyId", user.CompanyId.ToString()),
                new Claim("companyName", companyName ?? string.Empty)
            };

            var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(securityToken);

            return (tokenString, expiresAt);
        }
    }
}
