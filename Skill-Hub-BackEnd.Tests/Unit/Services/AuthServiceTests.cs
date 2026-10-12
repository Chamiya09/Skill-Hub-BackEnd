using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Skill_Hub_BackEnd.DTOs.Admin;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Services.Candidates;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Services
{
    public class AuthServiceTests
    {
        private readonly ITokenService _tokenService;

        public AuthServiceTests()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            _tokenService = new TokenService(config);
        }

        [Fact]
        public async Task RegisterCandidateAsync_ValidDto_SuccessfullyCreatesCandidate()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var dto = new RegisterCandidateDto
            {
                FirstName = "Chamod",
                LastName = "Ekanayaka",
                Email = "chamod.ekanayaka@test.com",
                Password = "SecurePassword@2026",
                Headline = "Full-Stack Software Engineer",
                Phone = "+94712345678"
            };

            // Act
            var response = await authService.RegisterCandidateAsync(dto);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Token);
            Assert.Equal("CANDIDATE", response.User.Role);
            Assert.Equal("Chamod Ekanayaka", response.User.FullName);
            Assert.Equal("chamod.ekanayaka@test.com", response.User.Email);

            var userInDb = await context.Users.FindAsync(response.User.Id);
            Assert.NotNull(userInDb);
            Assert.True(BCrypt.Net.BCrypt.Verify(dto.Password, userInDb.PasswordHash));
        }

        [Fact]
        public async Task RegisterCandidateAsync_DuplicateEmail_ThrowsInvalidOperationException()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Existing User",
                Email = "duplicate@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass@123"),
                Role = "CANDIDATE"
            };
            context.Users.Add(existingUser);
            await context.SaveChangesAsync();

            var dto = new RegisterCandidateDto
            {
                FirstName = "Another",
                LastName = "User",
                Email = "duplicate@test.com",
                Password = "SomePassword123"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterCandidateAsync(dto));
            Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RegisterCompanyAsync_ValidDto_SuccessfullyCreatesCompany()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var dto = new RegisterCompanyDto
            {
                CompanyName = "Virtusa Corp",
                CompanyEmail = "talent@virtusa.test",
                Password = "EnterprisePass@2026",
                Industry = "IT Consulting",
                Website = "https://virtusa.test"
            };

            // Act
            var response = await authService.RegisterCompanyAsync(dto);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Token);
            Assert.Equal("Company", response.User.Role);
            Assert.Equal(dto.CompanyName, response.User.CompanyName);

            var companyInDb = await context.Companies.FindAsync(response.User.Id);
            Assert.NotNull(companyInDb);
            Assert.True(BCrypt.Net.BCrypt.Verify(dto.Password, companyInDb.PasswordHash));
        }

        [Fact]
        public async Task RegisterCompanyAsync_DuplicateEmail_ThrowsInvalidOperationException()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var existingCompany = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "Existing Co",
                ContactEmail = "existing@company.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123")
            };
            context.Companies.Add(existingCompany);
            await context.SaveChangesAsync();

            var dto = new RegisterCompanyDto
            {
                CompanyName = "Duplicate Co",
                CompanyEmail = "existing@company.test",
                Password = "NewPass@123"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterCompanyAsync(dto));
            Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginCandidateAsync_ValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var rawPassword = "CandidateSecret@123";
            var candidate = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Candidate One",
                Email = "candidate1@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword),
                Role = "CANDIDATE"
            };
            context.Users.Add(candidate);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Email = "candidate1@test.com",
                Password = rawPassword
            };

            // Act
            var result = await authService.LoginCandidateAsync(loginDto);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.Equal(candidate.Id, result.User.Id);
            Assert.Equal("CANDIDATE", result.User.Role);
        }

        [Fact]
        public async Task LoginCandidateAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var candidate = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Candidate Two",
                Email = "candidate2@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPassword@123"),
                Role = "CANDIDATE"
            };
            context.Users.Add(candidate);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Email = "candidate2@test.com",
                Password = "WrongPassword@999"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.LoginCandidateAsync(loginDto));
            Assert.Contains("invalid email or password", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsync_ValidCompanyCredentials_ReturnsCompanyAuthResponse()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var rawPassword = "CompanySecret@123";
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "Stripe Payments",
                ContactEmail = "stripe@payments.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword)
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Email = "stripe@payments.test",
                Password = rawPassword
            };

            // Act
            var result = await authService.LoginAsync(loginDto);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.Equal("Company", result.User.Role);
            Assert.Equal(company.Id, result.User.Id);
        }

        [Fact]
        public async Task LoginGeneralAsync_BothCandidateAndEmployer_ResolvesCorrectProfile()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var authService = new AuthService(context, _tokenService);

            var userPass = "UserPass@123";
            var candidateUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Universal Candidate",
                Email = "universal.cand@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(userPass),
                Role = "CANDIDATE"
            };
            context.Users.Add(candidateUser);

            var compPass = "CompPass@123";
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "Universal Enterprise",
                ContactEmail = "universal.comp@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(compPass)
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();

            // Act 1: Login candidate via universal endpoint
            var userResult = await authService.LoginGeneralAsync(new LoginDto { Email = "universal.cand@test.com", Password = userPass });
            Assert.Equal("CANDIDATE", userResult.User.Role);

            // Act 2: Login company via universal endpoint
            var compResult = await authService.LoginGeneralAsync(new LoginDto { Email = "universal.comp@test.com", Password = compPass });
            Assert.Equal("Company", compResult.User.Role);
        }
    }
}
