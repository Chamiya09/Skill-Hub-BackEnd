using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Assessments;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class JobVacanciesControllerTests
    {
        private readonly Mock<ILogger<JobVacanciesController>> _loggerMock;

        public JobVacanciesControllerTests()
        {
            _loggerMock = new Mock<ILogger<JobVacanciesController>>();
        }

        private static JobVacanciesController CreateController(
            Skill_Hub_BackEnd.Data.ApplicationDbContext context,
            Guid? companyId = null,
            Guid? userId = null,
            ILogger<JobVacanciesController>? logger = null)
        {
            var controller = new JobVacanciesController(context, logger ?? new Mock<ILogger<JobVacanciesController>>().Object);
            var claims = new List<Claim>();

            if (companyId.HasValue)
            {
                claims.Add(new Claim("companyId", companyId.Value.ToString()));
                claims.Add(new Claim(ClaimTypes.Role, "Company"));
                claims.Add(new Claim(ClaimTypes.NameIdentifier, companyId.Value.ToString()));
            }

            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim(ClaimTypes.Role, "CANDIDATE"));
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };
            return controller;
        }

        [Fact]
        public async Task CreateJob_ValidDto_CreatesJobAndReturns201Created()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            context.Companies.Add(new Company { Id = companyId, CompanyName = "Tech Corp", ContactEmail = "hr@techcorp.test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, companyId: companyId);

            var dto = new CreateJobDto
            {
                Title = "Backend Architect",
                Department = "Engineering",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Senior",
                SalaryRange = "$150,000 - $180,000",
                Description = "Seeking an experienced backend architect."
            };

            // Act
            var result = await controller.CreateJob(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<JobResponseDto>(createdResult.Value);
            Assert.Equal("Backend Architect", response.Title);
            Assert.Equal(companyId, response.CompanyId);

            var inDb = await context.JobVacancies.FindAsync(response.Id);
            Assert.NotNull(inDb);
            Assert.Equal("Active", inDb.Status);
        }

        [Fact]
        public async Task GetJobById_BelongsToAnotherCompany_ReturnsNotFound404()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var myCompanyId = Guid.NewGuid();
            var otherCompanyId = Guid.NewGuid();

            var job = new JobVacancy
            {
                Id = Guid.NewGuid(),
                CompanyId = otherCompanyId,
                Title = "Confidential Role",
                Department = "Security",
                Location = "On-site",
                EmploymentType = "Full-time",
                ExperienceLevel = "Staff",
                Status = "Active"
            };
            context.JobVacancies.Add(job);
            await context.SaveChangesAsync();

            var controller = CreateController(context, companyId: myCompanyId);

            // Act
            var result = await controller.GetJobById(job.Id);

            // Assert: Cannot access a job vacancy owned by another company
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteJob_NoCompletedSubmissions_PermanentlyDeletesJob()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var jobId = Guid.NewGuid();

            context.JobVacancies.Add(new JobVacancy
            {
                Id = jobId,
                CompanyId = companyId,
                Title = "Draft Role",
                Department = "Sales",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Junior",
                Status = "Active"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, companyId: companyId);

            // Act
            var result = await controller.DeleteJob(jobId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var inDb = await context.JobVacancies.FindAsync(jobId);
            Assert.Null(inDb);
        }

        [Fact]
        public async Task DeleteJob_WithCompletedSubmissions_SoftDeletesPreservingData()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();

            context.JobVacancies.Add(new JobVacancy
            {
                Id = jobId,
                CompanyId = companyId,
                Title = "Assessed Role",
                Department = "Engineering",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Mid",
                Status = "Active"
            });

            var assessment = new Assessment
            {
                Id = Guid.NewGuid(),
                JobVacancyId = jobId,
                Title = "Assessed Challenge",
                CreatedBy = companyId
            };
            context.Assessments.Add(assessment);

            var submissionId = Guid.NewGuid();
            context.Submissions.Add(new Submission
            {
                Id = submissionId,
                AssessmentId = assessment.Id,
                JobVacancyId = jobId,
                CandidateId = candidateId,
                Status = "Passed",
                ExamScore = 95
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, companyId: companyId);

            // Act
            var result = await controller.DeleteJob(jobId);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var inDb = await context.JobVacancies.FindAsync(jobId);
            Assert.NotNull(inDb);
            Assert.Equal("Deleted", inDb.Status); // Preserved with soft-delete!

            var submissionInDb = await context.Submissions.FindAsync(submissionId);
            Assert.NotNull(submissionInDb);
            Assert.Equal("Passed", submissionInDb.Status);
        }

        [Fact]
        public async Task ApplyForJob_ValidApplication_CreatesJobApplicationAndReturns201()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();

            context.Companies.Add(new Company { Id = companyId, CompanyName = "Partner Co", ContactEmail = "partner@co.test" });
            context.Users.Add(new User { Id = candidateId, FullName = "Applicant One", Email = "applicant@test.com", Role = "CANDIDATE" });
            context.JobVacancies.Add(new JobVacancy
            {
                Id = jobId,
                CompanyId = companyId,
                Title = "Senior Software Architect",
                Department = "Engineering",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Senior",
                Status = "Active"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId: candidateId);

            var applyDto = new ApplyJobDto { CoverNote = "I am thrilled to apply for this engineering role." };

            // Act
            var result = await controller.ApplyForJob(jobId, applyDto);

            // Assert
            var createdResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);

            var application = Assert.IsType<CandidateApplicationResponseDto>(createdResult.Value);
            Assert.Equal("Senior Software Architect", application.JobTitle);
            Assert.Equal("Applied", application.Status);
        }

        [Fact]
        public async Task ApplyForJob_DuplicateApplication_ReturnsConflict409()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();

            context.Companies.Add(new Company { Id = companyId, CompanyName = "Company Two", ContactEmail = "comp2@test.com" });
            context.Users.Add(new User { Id = candidateId, FullName = "Applicant Two", Email = "applicant2@test.com", Role = "CANDIDATE" });
            context.JobVacancies.Add(new JobVacancy
            {
                Id = jobId,
                CompanyId = companyId,
                Title = "Product Manager",
                Department = "Product",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Lead",
                Status = "Active"
            });
            context.JobApplications.Add(new JobApplication
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                CandidateId = candidateId,
                Status = "Applied",
                AppliedDate = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId: candidateId);

            // Act: apply again for the same job
            var result = await controller.ApplyForJob(jobId, new ApplyJobDto());

            // Assert: Must reject duplicate application
            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task ApplyForJob_ClosedJob_ReturnsBadRequest400()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();

            context.Companies.Add(new Company { Id = companyId, CompanyName = "Company Three", ContactEmail = "comp3@test.com" });
            context.Users.Add(new User { Id = candidateId, FullName = "Applicant Three", Email = "app3@test.com", Role = "CANDIDATE" });
            context.JobVacancies.Add(new JobVacancy
            {
                Id = jobId,
                CompanyId = companyId,
                Title = "Closed Vacancy",
                Department = "Legal",
                Location = "On-site",
                EmploymentType = "Part-time",
                ExperienceLevel = "Senior",
                Status = "Closed"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId: candidateId);

            // Act
            var result = await controller.ApplyForJob(jobId, new ApplyJobDto());

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
