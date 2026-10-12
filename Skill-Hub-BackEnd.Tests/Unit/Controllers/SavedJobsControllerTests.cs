using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Models.Admin;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Models.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class SavedJobsControllerTests
    {
        private static SavedJobsController CreateController(
            Skill_Hub_BackEnd.Data.ApplicationDbContext context,
            Guid candidateId)
        {
            var controller = new SavedJobsController(context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(candidateId, "candidate@test.com", "CANDIDATE")
                }
            };
            return controller;
        }

        [Fact]
        public async Task SaveJob_ActiveJob_SavesSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var candidateId = Guid.NewGuid();
            var jobId = Guid.NewGuid();

            context.Users.Add(new User { Id = candidateId, FullName = "Candidate One", Email = "cand1@test.com", Role = "CANDIDATE" });
            context.JobVacancies.Add(new JobVacancy
            {
                Id = jobId,
                CompanyId = Guid.NewGuid(),
                Title = "Cloud Architect",
                Department = "Engineering",
                Location = "Remote",
                EmploymentType = "Full-time",
                ExperienceLevel = "Senior",
                Status = "Active"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, candidateId);

            // Act
            var result = await controller.SaveJob(jobId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var inDb = await context.SavedJobs.FirstOrDefaultAsync(s => s.CandidateId == candidateId && s.JobId == jobId);
            Assert.NotNull(inDb);
        }

        [Fact]
        public async Task SaveJob_NonExistentJob_ReturnsNotFound404()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var candidateId = Guid.NewGuid();
            var controller = CreateController(context, candidateId);

            // Act
            var result = await controller.SaveJob(Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task RemoveSavedJob_ExistingSavedJob_RemovesBookmark()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var candidateId = Guid.NewGuid();
            var jobId = Guid.NewGuid();

            context.SavedJobs.Add(new SavedJob { CandidateId = candidateId, JobId = jobId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, candidateId);

            // Act
            var result = await controller.RemoveSavedJob(jobId, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var inDb = await context.SavedJobs.FirstOrDefaultAsync(s => s.CandidateId == candidateId && s.JobId == jobId);
            Assert.Null(inDb);
        }
    }
}
