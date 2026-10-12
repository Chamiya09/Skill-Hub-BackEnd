using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Admin;
using Skill_Hub_BackEnd.DTOs.Admin;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Interviews;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Services.Candidates;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class AdminControllerTests
    {
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<ILogger<AdminController>> _loggerMock;

        public AdminControllerTests()
        {
            _tokenServiceMock = new Mock<ITokenService>();
            _loggerMock = new Mock<ILogger<AdminController>>();
        }

        private AdminController CreateController(
            Skill_Hub_BackEnd.Data.ApplicationDbContext context,
            string role = "Admin")
        {
            var controller = new AdminController(context, _tokenServiceMock.Object, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(Guid.NewGuid(), "admin@skillhub.internal", role)
                }
            };
            return controller;
        }

        [Fact]
        public async Task GetDashboardStats_AdminRole_ReturnsSystemMetrics()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            context.Users.Add(new User { Id = Guid.NewGuid(), FullName = "Candidate 1", Email = "c1@test.com", Role = "CANDIDATE" });
            context.JobVacancies.Add(new JobVacancy { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Title = "Dev Job", Status = "Active" });
            context.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "Test Co", ContactEmail = "co@test.com" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, "Admin");

            // Act
            var result = await controller.GetDashboardStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var stats = Assert.IsType<AdminDashboardStatsDto>(okResult.Value);
            Assert.True(stats.TotalCandidates >= 1);
            Assert.True(stats.ActiveJobs >= 1);
            Assert.True(stats.TotalCompanies >= 1);
            Assert.Equal(99.98, stats.SystemUptimePercent);
        }

        [Fact]
        public async Task AdminLogin_ValidCredentials_ReturnsAdminJwtToken()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();

            _tokenServiceMock.Setup(s => s.GenerateToken(It.IsAny<User>(), It.IsAny<string?>()))
                .Returns(("admin-jwt-token-2026", DateTime.UtcNow.AddHours(24)));

            var controller = CreateController(context);

            var loginRequest = new AdminLoginRequestDto
            {
                Email = "admin@skillhub.internal",
                Password = "SkillHub@Admin2026"
            };

            // Act
            var result = await controller.AdminLogin(loginRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var authResponse = Assert.IsType<AuthResponseDto>(okResult.Value);
            Assert.Equal("admin-jwt-token-2026", authResponse.Token);
            Assert.Equal("Admin", authResponse.User.Role);
        }

        [Fact]
        public async Task AdminLogin_InvalidPassword_ReturnsUnauthorized401()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = CreateController(context);

            var loginRequest = new AdminLoginRequestDto
            {
                Email = "admin@skillhub.internal",
                Password = "WrongPassword@123"
            };

            // Act
            var result = await controller.AdminLogin(loginRequest);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task AdminLogin_MissingCredentials_ReturnsBadRequest400()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.AdminLogin(new AdminLoginRequestDto { Email = "", Password = "" });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ToggleUserSuspend_CandidateUser_TogglesSuspensionStatus()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FullName = "Suspended Candidate",
                Email = "suspend.me@test.com",
                Role = "CANDIDATE",
                IsSuspended = false
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act: Toggle 1: False -> True (Suspended)
            var result1 = await controller.ToggleUserSuspend(userId.ToString());
            var okResult1 = Assert.IsType<OkObjectResult>(result1);

            var inDb1 = await context.Users.FindAsync(userId);
            Assert.NotNull(inDb1);
            Assert.True(inDb1.IsSuspended);

            // Act: Toggle 2: True -> False (Reactivated)
            var result2 = await controller.ToggleUserSuspend(userId.ToString());
            var okResult2 = Assert.IsType<OkObjectResult>(result2);

            var inDb2 = await context.Users.FindAsync(userId);
            Assert.NotNull(inDb2);
            Assert.False(inDb2.IsSuspended);
        }

        [Fact]
        public async Task ToggleUserSuspend_NonExistentUser_ReturnsNotFound404()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.ToggleUserSuspend(Guid.NewGuid().ToString());

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetInquiries_ReturnsPersistedInquiries()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            context.ContactInquiries.Add(new ContactInquiry
            {
                Id = Guid.NewGuid(),
                Sender = "University Dean",
                Email = "dean@university.edu",
                Subject = "Academic Partnership Inquiry",
                Message = "Interested in integrating Skill Hub into our CS curriculum.",
                Status = "New",
                Priority = "High",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetInquiries();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var inquiries = Assert.IsAssignableFrom<List<AdminInquiryDto>>(okResult.Value);
            Assert.Single(inquiries);
            Assert.Equal("University Dean", inquiries[0].Sender);
        }
    }
}
