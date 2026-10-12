using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Skill_Hub_BackEnd.Controllers.Jobs;
using Skill_Hub_BackEnd.DTOs.Jobs;
using Skill_Hub_BackEnd.Services.Jobs;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class ApplicantScreeningControllerTests
    {
        private readonly Mock<IApplicantScreeningService> _screeningServiceMock;

        public ApplicantScreeningControllerTests()
        {
            _screeningServiceMock = new Mock<IApplicantScreeningService>();
        }

        private ApplicantScreeningController CreateController(
            Skill_Hub_BackEnd.Data.ApplicationDbContext context,
            Guid companyId)
        {
            var controller = new ApplicantScreeningController(context, _screeningServiceMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(companyId, "hr@company.test", "HR_Admin", companyId: companyId)
                }
            };
            return controller;
        }

        [Fact]
        public async Task RunAiScreen_ValidJob_ReturnsRankedApplicants()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateInMemoryDbContext();
            var companyId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var controller = CreateController(context, companyId);

            var rankedList = new List<ScreenedApplicantDto>
            {
                new ScreenedApplicantDto
                {
                    CandidateId = Guid.NewGuid(),
                    FullName = "Top Candidate",
                    AiMatchScore = 96
                }
            };

            _screeningServiceMock.Setup(s => s.FetchAndRankApplicantsAsync(
                jobId,
                companyId,
                false,
                true,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(rankedList);

            // Act
            var result = await controller.RunAiScreen(jobId, false, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsAssignableFrom<IReadOnlyList<ScreenedApplicantDto>>(okResult.Value);
            Assert.Single(returned);
            Assert.Equal(96, returned[0].AiMatchScore);
        }
    }
}
