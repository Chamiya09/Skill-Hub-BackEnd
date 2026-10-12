using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class CompaniesControllerTests
    {
        private readonly Mock<ILogger<CompaniesController>> _loggerMock;

        public CompaniesControllerTests()
        {
            _loggerMock = new Mock<ILogger<CompaniesController>>();
        }

        [Fact]
        public async Task GetCompanyByIdOrName_ValidGuid_ReturnsCompanyWithActiveJobs()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var company = new Company
            {
                Id = companyId,
                CompanyName = "Linear Systems",
                ContactEmail = "careers@linear.test",
                Industry = "DevTools",
                Website = "https://linear.test"
            };
            context.Companies.Add(company);

            context.JobVacancies.Add(new JobVacancy
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Title = "Frontend Engineer",
                Department = "Engineering",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Senior",
                Status = "Active"
            });
            await context.SaveChangesAsync();

            var controller = new CompaniesController(context, _loggerMock.Object);

            // Act
            var result = await controller.GetCompanyByIdOrName(companyId.ToString());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetCompanyByIdOrName_BySlugifiedName_ReturnsMatchingCompany()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "Stripe Payments",
                ContactEmail = "stripe@payments.test",
                Industry = "FinTech"
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();

            var controller = new CompaniesController(context, _loggerMock.Object);

            // Act: query with slug "stripe-payments"
            var result = await controller.GetCompanyByIdOrName("stripe-payments");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetCompanyByIdOrName_NonExistentCompany_ReturnsNotFound404()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var controller = new CompaniesController(context, _loggerMock.Object);

            // Act
            var result = await controller.GetCompanyByIdOrName("non-existent-company-xyz");

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
