using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Interviews;
using Skill_Hub_BackEnd.DTOs.Interviews;
using Skill_Hub_BackEnd.Services.Interviews;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class InterviewPrepControllerTests
    {
        private readonly Mock<IInterviewPrepService> _interviewPrepServiceMock;
        private readonly Mock<ILogger<InterviewPrepController>> _loggerMock;

        public InterviewPrepControllerTests()
        {
            _interviewPrepServiceMock = new Mock<IInterviewPrepService>();
            _loggerMock = new Mock<ILogger<InterviewPrepController>>();
        }

        private InterviewPrepController CreateController(Guid candidateId)
        {
            var controller = new InterviewPrepController(_interviewPrepServiceMock.Object, _loggerMock.Object);
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
        public async Task Generate_ValidRequest_ReturnsOkWithGuide()
        {
            // Arrange
            var candidateId = Guid.NewGuid();
            var controller = CreateController(candidateId);

            var request = new GenerateInterviewPrepRequestDto
            {
                ApplicationId = Guid.NewGuid(),
                TargetRole = "Full-Stack Engineer",
                JobDescription = "React and .NET Core platform development."
            };

            var guideDetail = new InterviewPrepGuideDto
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateId,
                TargetRole = "Full-Stack Engineer",
                CompanyName = "Skill Hub Partner"
            };

            _interviewPrepServiceMock.Setup(s => s.GenerateGuideAsync(candidateId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(guideDetail);

            // Act
            var result = await controller.Generate(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<GenerateInterviewPrepResponseDto>(okResult.Value);
            Assert.Equal(guideDetail.Id, response.GuideId);
        }

        [Fact]
        public async Task Generate_RejectedApplication_ReturnsForbidden403()
        {
            // Arrange
            var candidateId = Guid.NewGuid();
            var controller = CreateController(candidateId);

            var request = new GenerateInterviewPrepRequestDto
            {
                ApplicationId = Guid.NewGuid(),
                TargetRole = "Senior Architect"
            };

            _interviewPrepServiceMock.Setup(s => s.GenerateGuideAsync(candidateId, request, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InterviewPrepIneligibleException("Candidate application was Rejected for this vacancy.", 403, "Rejected"));

            // Act
            var result = await controller.Generate(request, CancellationToken.None);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        }

        [Fact]
        public async Task GetById_GuideExists_ReturnsOk200()
        {
            // Arrange
            var candidateId = Guid.NewGuid();
            var guideId = Guid.NewGuid();
            var controller = CreateController(candidateId);

            var guide = new InterviewPrepGuideDto { Id = guideId, CandidateId = candidateId };
            _interviewPrepServiceMock.Setup(s => s.GetGuideByIdAsync(guideId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(guide);

            // Act
            var result = await controller.GetById(guideId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(guide, okResult.Value);
        }
    }
}
