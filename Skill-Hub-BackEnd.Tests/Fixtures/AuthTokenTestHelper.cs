using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Skill_Hub_BackEnd.Tests.Fixtures
{
    public static class AuthTokenTestHelper
    {
        public const string DefaultSecretKey = "SkillHub_Super_Secret_Production_Security_Key_2026_ATS_AI_Matching_Engine_Secure!";
        public const string DefaultIssuer = "SkillHubApi";
        public const string DefaultAudience = "SkillHubClients";

        public static string GenerateTestToken(
            Guid userId,
            string email,
            string role,
            Guid? companyId = null,
            string? name = null,
            int expirationMinutes = 60,
            string? secretKey = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey ?? DefaultSecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, name ?? email),
                new Claim("name", name ?? email),
                new Claim(ClaimTypes.Role, role),
                new Claim("role", role)
            };

            if (companyId.HasValue)
            {
                claims.Add(new Claim("companyId", companyId.Value.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Issuer = DefaultIssuer,
                Audience = DefaultAudience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(securityToken);
        }

        public static string GenerateExpiredToken(Guid userId, string email, string role)
        {
            return GenerateTestToken(userId, email, role, expirationMinutes: -120);
        }

        public static ClaimsPrincipal CreateClaimsPrincipal(
            Guid userId,
            string email,
            string role,
            Guid? companyId = null,
            string? name = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("role", role),
                new Claim(ClaimTypes.Name, name ?? email)
            };

            if (companyId.HasValue)
            {
                claims.Add(new Claim("companyId", companyId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }
    }
}
