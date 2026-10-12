using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Candidates;
using Skill_Hub_BackEnd.DTOs.Admin;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Services.Candidates;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class CandidateAuthControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly Mock<ILogger<CandidateAuthController>> _loggerMock;

        public CandidateAuthControllerTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _loggerMock = new Mock<ILogger<CandidateAuthController>>();
        }

        [Fact]
        public async Task RegisterCandidate_ValidDto_ReturnsCreated201()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var dto = new RegisterCandidateDto
            {
                FirstName = "Chamod",
                LastName = "Ekanayaka",
                Email = "test@skillhub.internal",
                Password = "Password@123"
            };

            var expectedResponse = new AuthResponseDto
            {
                Token = "fake-jwt-token",
                User = new UserResponseDto { Id = Guid.NewGuid(), Email = dto.Email, Role = "CANDIDATE" }
            };

            _authServiceMock.Setup(s => s.RegisterCandidateAsync(dto)).ReturnsAsync(expectedResponse);

            // Act
            var result = await controller.RegisterCandidate(dto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
            Assert.Equal(expectedResponse, objectResult.Value);
        }

        [Fact]
        public async Task RegisterCandidate_InvalidModelState_ReturnsBadRequest400()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);
            controller.ModelState.AddModelError("Email", "The Email field is required.");

            var dto = new RegisterCandidateDto();

            // Act
            var result = await controller.RegisterCandidate(dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RegisterCandidate_DuplicateEmail_ReturnsConflict409()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var dto = new RegisterCandidateDto { Email = "duplicate@test.com", Password = "Pass" };
            _authServiceMock.Setup(s => s.RegisterCandidateAsync(dto))
                .ThrowsAsync(new InvalidOperationException("An account with the email 'duplicate@test.com' is already registered."));

            // Act
            var result = await controller.RegisterCandidate(dto);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.NotNull(conflictResult.Value);
        }

        [Fact]
        public async Task LoginCandidate_ValidCredentials_ReturnsOk200()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var loginDto = new LoginDto { Email = "user@test.com", Password = "ValidPassword" };
            var authResponse = new AuthResponseDto
            {
                Token = "valid-token",
                User = new UserResponseDto { Email = loginDto.Email, Role = "CANDIDATE" }
            };

            _authServiceMock.Setup(s => s.LoginCandidateAsync(loginDto)).ReturnsAsync(authResponse);

            // Act
            var result = await controller.LoginCandidate(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(authResponse, okResult.Value);
        }

        [Fact]
        public async Task LoginCandidate_InvalidCredentials_ReturnsUnauthorized401()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var loginDto = new LoginDto { Email = "user@test.com", Password = "BadPassword" };
            _authServiceMock.Setup(s => s.LoginCandidateAsync(loginDto))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid email or password credentials."));

            // Act
            var result = await controller.LoginCandidate(loginDto);

            // Assert
            var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.NotNull(unauthResult.Value);
        }

        [Fact]
        public async Task GetCurrentCandidate_WithValidClaims_ReturnsUserProfile()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FullName = "Authenticated Candidate",
                Email = "auth.cand@test.com",
                Role = "CANDIDATE",
                Headline = "Lead Architect"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(userId, user.Email, "CANDIDATE")
                }
            };

            // Act
            var result = await controller.GetCurrentCandidate();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseDto = Assert.IsType<UserResponseDto>(okResult.Value);
            Assert.Equal(userId, responseDto.Id);
            Assert.Equal(user.Email, responseDto.Email);
        }

        [Fact]
        public async Task GetCurrentCandidate_MissingUserIdentifier_ReturnsUnauthorized401()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CandidateAuthController(_authServiceMock.Object, context, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await controller.GetCurrentCandidate();

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}
