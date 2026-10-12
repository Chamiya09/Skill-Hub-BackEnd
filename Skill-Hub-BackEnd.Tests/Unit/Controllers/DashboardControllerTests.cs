using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class DashboardControllerTests
    {
        private readonly Mock<ILogger<DashboardController>> _loggerMock;

        public DashboardControllerTests()
        {
            _loggerMock = new Mock<ILogger<DashboardController>>();
        }

        [Fact]
        public async Task GetDashboardStats_AuthenticatedCompany_ReturnsAccurateMetrics()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var company = new Company
            {
                Id = companyId,
                CompanyName = "Scale AI Enterprise",
                ContactEmail = "talent@scaleai.test"
            };
            context.Companies.Add(company);

            var activeJob = new JobVacancy
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Title = "ML Infrastructure Engineer",
                Department = "Engineering",
                Status = "Active"
            };
            var closedJob = new JobVacancy
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Title = "Archived Position",
                Department = "Marketing",
                Status = "Closed"
            };
            context.JobVacancies.AddRange(activeJob, closedJob);

            var candidate = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Applicant One",
                Email = "app1@test.com",
                Role = "CANDIDATE"
            };
            context.Users.Add(candidate);

            context.JobApplications.Add(new JobApplication
            {
                Id = Guid.NewGuid(),
                JobId = activeJob.Id,
                CandidateId = candidate.Id,
                AppliedDate = DateTime.UtcNow,
                Status = "Shortlisted"
            });

            await context.SaveChangesAsync();

            var controller = new DashboardController(context, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(companyId, company.ContactEmail, "Company", companyId: companyId)
                }
            };

            // Act
            var result = await controller.GetDashboardStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var stats = Assert.IsType<DashboardStatsDto>(okResult.Value);

            Assert.Equal(1, stats.ActiveVacanciesCount);
            Assert.Equal(1, stats.ClosedVacanciesCount);
            Assert.Equal(2, stats.TotalVacanciesCount);
            Assert.Equal(1, stats.TotalCandidatesCount);
            Assert.Equal(1, stats.ShortlistedCount);
        }

        [Fact]
        public async Task GetDashboardStats_MissingCompanyClaim_ReturnsUnauthorized401()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new DashboardController(context, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await controller.GetDashboardStats();

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}
