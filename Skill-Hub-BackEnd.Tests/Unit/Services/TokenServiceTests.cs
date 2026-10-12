using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Services.Candidates;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Services
{
    public class TokenServiceTests
    {
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;

        public TokenServiceTests()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "JwtSettings:SecretKey", "SkillHub_Super_Secret_Production_Security_Key_2026_ATS_AI_Matching_Engine_Secure!" },
                { "JwtSettings:Issuer", "SkillHubApi" },
                { "JwtSettings:Audience", "SkillHubClients" },
                { "JwtSettings:ExpirationInHours", "24" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _tokenService = new TokenService(_configuration);
        }

        [Fact]
        public void GenerateToken_ForCompany_ReturnsValidJwtWithCompanyClaims()
        {
            // Arrange
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "Acme Corp",
                ContactEmail = "contact@acme.test"
            };

            // Act
            var (token, expiresAt) = _tokenService.GenerateToken(company);

            // Assert
            Assert.NotNull(token);
            Assert.True(expiresAt > DateTime.UtcNow);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            Assert.Equal("SkillHubApi", jwtToken.Issuer);
            Assert.Contains(jwtToken.Audiences, a => a == "SkillHubClients");

            var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
            Assert.NotNull(roleClaim);
            Assert.Equal("Company", roleClaim.Value);

            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == JwtRegisteredClaimNames.Email);
            Assert.NotNull(emailClaim);
            Assert.Equal(company.ContactEmail, emailClaim.Value);

            var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier);
            Assert.NotNull(subClaim);
            Assert.Equal(company.Id.ToString(), subClaim.Value);
        }

        [Theory]
        [InlineData("CANDIDATE")]
        [InlineData("Admin")]
        [InlineData("HR_Admin")]
        public void GenerateToken_ForUser_IncludesExpectedRoleAndIdClaims(string role)
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Test Candidate",
                Email = "candidate@skillhub.test",
                Role = role,
                CompanyId = Guid.NewGuid()
            };

            // Act
            var (token, expiresAt) = _tokenService.GenerateToken(user, "Test Enterprise");

            // Assert
            Assert.NotNull(token);
            Assert.True(expiresAt > DateTime.UtcNow);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var roles = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value);
            Assert.Contains(role, roles);

            var companyClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "companyId");
            Assert.NotNull(companyClaim);
            Assert.Equal(user.CompanyId.ToString(), companyClaim.Value);
        }

        [Fact]
        public void GenerateToken_FallbackConfiguration_UsesDefaultSettingsGracefully()
        {
            // Arrange: Empty configuration to test fallback defaults
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var fallbackService = new TokenService(emptyConfig);

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Default User",
                Email = "default@skillhub.test",
                Role = "CANDIDATE"
            };

            // Act
            var (token, expiresAt) = fallbackService.GenerateToken(user, null);

            // Assert
            Assert.NotNull(token);
            Assert.True(expiresAt > DateTime.UtcNow);
        }
    }
}
