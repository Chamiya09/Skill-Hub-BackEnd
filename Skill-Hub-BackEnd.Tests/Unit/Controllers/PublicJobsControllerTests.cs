using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class PublicJobsControllerTests
    {
        private readonly Mock<ILogger<PublicJobsController>> _loggerMock;

        public PublicJobsControllerTests()
        {
            _loggerMock = new Mock<ILogger<PublicJobsController>>();
        }

        [Fact]
        public async Task GetPublicJobs_FiltersBySearchTermAndDepartment()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            context.Companies.Add(new Company { Id = companyId, CompanyName = "Virtusa Global", ContactEmail = "hr@virtusa.test" });

            context.JobVacancies.AddRange(
                new JobVacancy
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Title = "Full-Stack React Engineer",
                    Department = "Engineering",
                    Location = "Colombo, Sri Lanka",
                    EmploymentType = "Full-time",
                    ExperienceLevel = "Mid",
                    Status = "Active"
                },
                new JobVacancy
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Title = "Head of HR",
                    Department = "Human Resources",
                    Location = "London, UK",
                    EmploymentType = "Full-time",
                    ExperienceLevel = "Director",
                    Status = "Active"
                },
                new JobVacancy
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Title = "Hidden Draft Job",
                    Department = "Engineering",
                    Location = "Remote",
                    EmploymentType = "Full-time",
                    ExperienceLevel = "Senior",
                    Status = "Draft" // Should be excluded from public list
                }
            );
            await context.SaveChangesAsync();

            var controller = new PublicJobsController(context, _loggerMock.Object);

            // Act 1: Search for "React"
            var searchResult = await controller.GetPublicJobs(search: "React");
            var okResult1 = Assert.IsType<OkObjectResult>(searchResult);
            var jobs1 = Assert.IsAssignableFrom<IEnumerable<JobResponseDto>>(okResult1.Value).ToList();
            Assert.Single(jobs1);
            Assert.Equal("Full-Stack React Engineer", jobs1[0].Title);

            // Act 2: Filter by department "Engineering"
            var deptResult = await controller.GetPublicJobs(department: "Engineering");
            var okResult2 = Assert.IsType<OkObjectResult>(deptResult);
            var jobs2 = Assert.IsAssignableFrom<IEnumerable<JobResponseDto>>(okResult2.Value).ToList();
            Assert.Single(jobs2); // only the Active one, not the Draft!
            Assert.Equal("Full-Stack React Engineer", jobs2[0].Title);
        }
    }
}
