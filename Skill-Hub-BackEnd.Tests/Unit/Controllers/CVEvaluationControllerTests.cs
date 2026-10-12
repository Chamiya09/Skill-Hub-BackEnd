using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Candidates;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.Services.Candidates;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class CVEvaluationControllerTests
    {
        private readonly Mock<IAgenticCvService> _agenticCvServiceMock;
        private readonly Mock<ILogger<CVEvaluationController>> _loggerMock;

        public CVEvaluationControllerTests()
        {
            _agenticCvServiceMock = new Mock<IAgenticCvService>();
            _loggerMock = new Mock<ILogger<CVEvaluationController>>();
        }

        private CVEvaluationController CreateController(Guid recruiterId)
        {
            var controller = new CVEvaluationController(_agenticCvServiceMock.Object, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(recruiterId, "recruiter@skillhub.test", "Company", companyId: recruiterId)
                }
            };
            return controller;
        }

        [Fact]
        public async Task Analyze_ValidRequest_ReturnsEvaluationResult()
        {
            // Arrange
            var recruiterId = Guid.NewGuid();
            var controller = CreateController(recruiterId);

            var request = new AnalyzeCvRequestDto
            {
                CandidateId = Guid.NewGuid(),
                JobId = Guid.NewGuid()
            };

            var expectedResult = new CvEvaluationResultDto
            {
                Id = Guid.NewGuid(),
                CandidateId = request.CandidateId,
                JobId = request.JobId,
                MatchScore = 89,
                Recommendation = "Interview",
                ApprovalStatus = "Pending"
            };

            _agenticCvServiceMock.Setup(s => s.AnalyzeCvAsync(request, recruiterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await controller.Analyze(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(expectedResult, okResult.Value);
        }

        [Fact]
        public async Task Approve_HumanApproval_UpdatesStatusToApproved()
        {
            // Arrange
            var recruiterId = Guid.NewGuid();
            var evalId = Guid.NewGuid();
            var controller = CreateController(recruiterId);

            var approveRequest = new ApproveEvaluationRequestDto
            {
                Decision = "Approved",
                ReviewerNotes = "Candidate has verified skills."
            };

            var approvedResponse = new ApproveEvaluationResponseDto
            {
                EvaluationId = evalId,
                ApprovalStatus = "Approved",
                Message = "Evaluation approved successfully."
            };

            _agenticCvServiceMock.Setup(s => s.ApproveEvaluationAsync(evalId, approveRequest, recruiterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(approvedResponse);

            // Act
            var result = await controller.Approve(evalId, approveRequest, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var updated = Assert.IsType<ApproveEvaluationResponseDto>(okResult.Value);
            Assert.Equal("Approved", updated.ApprovalStatus);
        }
    }
}
