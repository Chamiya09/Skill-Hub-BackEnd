using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Candidates;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.Models.Candidates;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class CandidateCvControllerTests
    {
        private readonly Mock<ILogger<CandidateCvController>> _loggerMock;

        public CandidateCvControllerTests()
        {
            _loggerMock = new Mock<ILogger<CandidateCvController>>();
        }

        private static CandidateCvController CreateControllerWithUser(
            Skill_Hub_BackEnd.Data.ApplicationDbContext context,
            Guid userId,
            ILogger<CandidateCvController> logger)
        {
            var controller = new CandidateCvController(context, logger);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(userId, "candidate@test.com", "CANDIDATE")
                }
            };
            return controller;
        }

        [Fact]
        public async Task GetFullCv_AuthenticatedCandidate_ReturnsCompleteCvStructure()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();

            var candidate = new User
            {
                Id = userId,
                FullName = "Alexander Rivera",
                Email = "alexander@test.com",
                Role = "CANDIDATE",
                Headline = "Lead AI / ML Engineer",
                Location = "Austin, TX"
            };
            context.Users.Add(candidate);

            context.CandidateEducations.Add(new CandidateEducation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Institution = "University of Colombo",
                Degree = "B.Sc. in Computer Science",
                StartYear = "2018",
                EndYear = "2022"
            });

            context.CandidateExperiences.Add(new CandidateExperience
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Company = "Virtusa",
                Title = "Senior Software Engineer",
                StartDate = "2022-01-01",
                IsCurrent = true
            });

            context.CandidateSkills.Add(new CandidateSkill
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SkillName = "C# / .NET Core",
                Category = "Engineering"
            });

            await context.SaveChangesAsync();

            var controller = CreateControllerWithUser(context, userId, _loggerMock.Object);

            // Act
            var result = await controller.GetFullCv();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task AddEducation_ValidData_PersistsAndReturnsCreated()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            context.Users.Add(new User { Id = userId, FullName = "Jane Doe", Email = "jane@test.com", Role = "CANDIDATE" });
            await context.SaveChangesAsync();

            var controller = CreateControllerWithUser(context, userId, _loggerMock.Object);

            var dto = new CreateEducationDto
            {
                Institution = "Stanford University",
                Degree = "M.S. Software Engineering",
                FieldOfStudy = "Computer Science",
                StartYear = "2022",
                EndYear = "2024"
            };

            // Act
            var result = await controller.AddEducation(dto);

            // Assert
            var createdResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            var eduDto = Assert.IsType<EducationDto>(createdResult.Value);

            var eduInDb = await context.CandidateEducations.FindAsync(eduDto.Id);
            Assert.NotNull(eduInDb);
            Assert.Equal("Stanford University", eduInDb.Institution);
            Assert.Equal(userId, eduInDb.UserId);
        }

        [Fact]
        public async Task DeleteEducation_BelongsToUser_RemovesRecordSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var eduId = Guid.NewGuid();

            context.CandidateEducations.Add(new CandidateEducation
            {
                Id = eduId,
                UserId = userId,
                Institution = "MIT",
                Degree = "B.S."
            });
            await context.SaveChangesAsync();

            var controller = CreateControllerWithUser(context, userId, _loggerMock.Object);

            // Act
            var result = await controller.DeleteEducation(eduId);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var inDb = await context.CandidateEducations.FindAsync(eduId);
            Assert.Null(inDb);
        }

        [Fact]
        public async Task DeleteEducation_BelongsToAnotherCandidate_ReturnsNotFound()
        {
            // Arrange: Education belongs to user A, but user B tries to delete it
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var ownerId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();
            var eduId = Guid.NewGuid();

            context.CandidateEducations.Add(new CandidateEducation
            {
                Id = eduId,
                UserId = ownerId,
                Institution = "Harvard",
                Degree = "MBA"
            });
            await context.SaveChangesAsync();

            var controller = CreateControllerWithUser(context, attackerId, _loggerMock.Object);

            // Act
            var result = await controller.DeleteEducation(eduId);

            // Assert: Cannot access or delete another candidate's resource
            Assert.IsType<NotFoundObjectResult>(result);
            var stillInDb = await context.CandidateEducations.FindAsync(eduId);
            Assert.NotNull(stillInDb);
        }

        [Fact]
        public async Task AddSkill_ValidSkill_PersistsAndReturnsCreated()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var controller = CreateControllerWithUser(context, userId, _loggerMock.Object);

            var dto = new CreateSkillDto
            {
                SkillName = "PostgreSQL",
                Category = "Databases"
            };

            // Act
            var result = await controller.AddSkill(dto);

            // Assert
            var createdResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            Assert.NotNull(createdResult.Value);
        }

        [Fact]
        public async Task AddExperience_ValidExperience_PersistsSuccessfully()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var controller = CreateControllerWithUser(context, userId, _loggerMock.Object);

            var dto = new CreateExperienceDto
            {
                Company = "Amazon AWS",
                Title = "Cloud Infrastructure Engineer",
                Location = "Seattle, WA",
                StartDate = "2022-01-01",
                IsCurrent = true,
                Description = "Designed multi-region serverless architectures."
            };

            // Act
            var result = await controller.AddExperience(dto);

            // Assert
            var createdResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            Assert.NotNull(createdResult.Value);
        }
    }
}
