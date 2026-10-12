using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.DTOs.Admin;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Services.Candidates;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class CompanyAuthControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly Mock<ILogger<CompanyAuthController>> _loggerMock;

        public CompanyAuthControllerTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _loggerMock = new Mock<ILogger<CompanyAuthController>>();
        }

        [Fact]
        public async Task Register_ValidCompanyDto_ReturnsCreated201()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CompanyAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var dto = new RegisterCompanyDto
            {
                CompanyName = "Anthropic Research",
                CompanyEmail = "careers@anthropic.test",
                Password = "Password@123"
            };

            var expectedResponse = new AuthResponseDto
            {
                Token = "fake-company-token",
                User = new UserResponseDto { Id = Guid.NewGuid(), CompanyName = dto.CompanyName, Role = "Company" }
            };

            _authServiceMock.Setup(s => s.RegisterCompanyAsync(dto)).ReturnsAsync(expectedResponse);

            // Act
            var result = await controller.Register(dto);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
            Assert.Equal(expectedResponse, objectResult.Value);
        }

        [Fact]
        public async Task Register_DuplicateCompanyEmail_ReturnsConflict409()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CompanyAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var dto = new RegisterCompanyDto { CompanyName = "Duplicate Co", CompanyEmail = "existing@co.test", Password = "Pass" };
            _authServiceMock.Setup(s => s.RegisterCompanyAsync(dto))
                .ThrowsAsync(new InvalidOperationException("A company with the email 'existing@co.test' is already registered."));

            // Act
            var result = await controller.Register(dto);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.NotNull(conflictResult.Value);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOk200()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CompanyAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var loginDto = new LoginDto { Email = "corp@test.com", Password = "ValidPass" };
            var authResponse = new AuthResponseDto
            {
                Token = "valid-token",
                User = new UserResponseDto { Email = loginDto.Email, Role = "Company" }
            };

            _authServiceMock.Setup(s => s.LoginAsync(loginDto)).ReturnsAsync(authResponse);

            // Act
            var result = await controller.Login(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(authResponse, okResult.Value);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized401()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CompanyAuthController(_authServiceMock.Object, context, _loggerMock.Object);

            var loginDto = new LoginDto { Email = "corp@test.com", Password = "Wrong" };
            _authServiceMock.Setup(s => s.LoginAsync(loginDto))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid company credentials."));

            // Act
            var result = await controller.Login(loginDto);

            // Assert
            var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.NotNull(unauthResult.Value);
        }

        [Fact]
        public async Task GetCurrentCompany_AuthenticatedCompany_ReturnsProfile()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var company = new Company
            {
                Id = companyId,
                CompanyName = "Databricks Inc.",
                ContactEmail = "jobs@databricks.test",
                Industry = "Big Data"
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();

            var controller = new CompanyAuthController(_authServiceMock.Object, context, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(companyId, company.ContactEmail, "Company", companyId: companyId)
                }
            };

            // Act
            var result = await controller.GetCurrentCompany();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var responseDto = Assert.IsType<UserResponseDto>(okResult.Value);
            Assert.Equal(companyId, responseDto.Id);
            Assert.Equal(company.CompanyName, responseDto.CompanyName);
        }

        [Fact]
        public async Task UpdateProfile_ValidUpdate_UpdatesCompanyDataAndReturnsOk()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var company = new Company
            {
                Id = companyId,
                CompanyName = "Old Name",
                ContactEmail = "old@company.test",
                Industry = "Old Industry"
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();

            var controller = new CompanyAuthController(_authServiceMock.Object, context, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(companyId, company.ContactEmail, "Company", companyId: companyId)
                }
            };

            var updateDto = new UpdateCompanyProfileDto
            {
                CompanyName = "Updated Tech Ltd",
                Industry = "FinTech",
                Location = "New York, NY",
                Website = "https://updatedtech.test"
            };

            // Act
            var result = await controller.UpdateProfile(updateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var updatedDto = Assert.IsType<UserResponseDto>(okResult.Value);
            Assert.Equal("Updated Tech Ltd", updatedDto.CompanyName);
            Assert.Equal("FinTech", updatedDto.Industry);

            var inDb = await context.Companies.FindAsync(companyId);
            Assert.NotNull(inDb);
            Assert.Equal("Updated Tech Ltd", inDb.CompanyName);
        }
    }
}
