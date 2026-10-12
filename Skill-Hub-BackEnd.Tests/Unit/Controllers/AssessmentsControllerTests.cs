using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Assessments;
using Skill_Hub_BackEnd.DTOs.Assessments;
using Skill_Hub_BackEnd.Services.Assessments;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class AssessmentsControllerTests
    {
        private readonly Mock<IAssessmentService> _assessmentServiceMock;
        private readonly Mock<ILogger<AssessmentsController>> _loggerMock;

        public AssessmentsControllerTests()
        {
            _assessmentServiceMock = new Mock<IAssessmentService>();
            _loggerMock = new Mock<ILogger<AssessmentsController>>();
        }

        private AssessmentsController CreateController(Guid userId, string role = "Company")
        {
            var controller = new AssessmentsController(_assessmentServiceMock.Object, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(userId, "recruiter@skillhub.test", role, companyId: userId)
                }
            };
            return controller;
        }

        [Fact]
        public async Task CreateAssessmentManual_ValidDto_ReturnsCreatedAtAction201()
        {
            // Arrange
            var hrManagerId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, "Company");

            var dto = new CreateAssessmentManualDto
            {
                JobVacancyId = Guid.NewGuid(),
                Title = "Algorithms & Data Structures Challenge",
                PassingThreshold = 75,
                TimeLimitMinutes = 60
            };

            var responseDto = new AssessmentResponseDto
            {
                Id = Guid.NewGuid(),
                JobVacancyId = dto.JobVacancyId,
                Title = dto.Title,
                Status = "Draft",
                PassingThreshold = 75,
                TimeLimitMinutes = 60
            };

            _assessmentServiceMock.Setup(s => s.CreateAssessmentManualAsync(dto, hrManagerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseDto);

            // Act
            var result = await controller.CreateAssessmentManual(dto, CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(responseDto, createdResult.Value);
        }

        [Fact]
        public async Task PublishAssessment_ValidId_ReturnsOk200()
        {
            // Arrange
            var hrManagerId = Guid.NewGuid();
            var assessmentId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, "Company");

            var responseDto = new AssessmentResponseDto { Id = assessmentId, Status = "Published" };
            _assessmentServiceMock.Setup(s => s.PublishAssessmentAsync(assessmentId, hrManagerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseDto);

            // Act
            var result = await controller.PublishAssessment(assessmentId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var updated = Assert.IsType<AssessmentResponseDto>(okResult.Value);
            Assert.Equal("Published", updated.Status);
        }

        [Fact]
        public async Task GetExamPaper_CandidateAccess_ReturnsSanitizedPaper()
        {
            // Arrange
            var candidateId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();
            var controller = CreateController(candidateId, "CANDIDATE");

            var sanitizedPaper = new StartExamResponseDto
            {
                SubmissionId = submissionId,
                AssessmentTitle = "Sanitized Python Assessment",
                TimeLimitMinutes = 60
            };

            _assessmentServiceMock.Setup(s => s.GetExamPaperAsync(submissionId, candidateId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sanitizedPaper);

            // Act
            var result = await controller.GetExamPaper(submissionId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(sanitizedPaper, okResult.Value);
        }

        [Fact]
        public async Task StartExam_CandidateStarts_ReturnsExamPaper()
        {
            // Arrange
            var candidateId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();
            var controller = CreateController(candidateId, "CANDIDATE");

            var paper = new StartExamResponseDto
            {
                SubmissionId = submissionId,
                AssessmentTitle = "Live Coding Exam",
                StartedAt = DateTime.UtcNow
            };

            _assessmentServiceMock.Setup(s => s.StartExamAsync(submissionId, candidateId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(paper);

            // Act
            var result = await controller.StartExam(submissionId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(paper, okResult.Value);
        }

        [Fact]
        public async Task SubmitExam_CandidateSubmitsSolutions_ReturnsGradedSubmissionDetail()
        {
            // Arrange
            var candidateId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();
            var controller = CreateController(candidateId, "CANDIDATE");

            var requestDto = new SubmitAnswersRequestDto();
            var responseDto = new SubmissionDetailDto
            {
                Id = submissionId,
                Status = "Passed",
                ExamScore = 88.5m,
                FinalWeightedScore = 88.5m
            };

            _assessmentServiceMock.Setup(s => s.SubmitAnswersAsync(submissionId, requestDto, candidateId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseDto);

            // Act
            var result = await controller.SubmitExam(submissionId, requestDto, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var detail = Assert.IsType<SubmissionDetailDto>(okResult.Value);
            Assert.Equal("Passed", detail.Status);
            Assert.Equal(88.5m, detail.ExamScore);
        }

        [Fact]
        public async Task GetLeaderboard_HRRequestsRankedList_ReturnsRankedCandidates()
        {
            // Arrange
            var jobVacancyId = Guid.NewGuid();
            var controller = CreateController(Guid.NewGuid(), "Company");

            var leaderboard = new List<LeaderboardEntryDto>
            {
                new LeaderboardEntryDto { CandidateId = Guid.NewGuid(), CandidateName = "Top Rank 1", FinalWeightedScore = 98 },
                new LeaderboardEntryDto { CandidateId = Guid.NewGuid(), CandidateName = "Top Rank 2", FinalWeightedScore = 92 }
            };

            _assessmentServiceMock.Setup(s => s.GetLeaderboardAsync(jobVacancyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(leaderboard);

            // Act
            var result = await controller.GetLeaderboard(jobVacancyId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var entries = Assert.IsAssignableFrom<IReadOnlyList<LeaderboardEntryDto>>(okResult.Value);
            Assert.Equal(2, entries.Count);
        }

        [Fact]
        public async Task FinalizeTop5_HRFinalizesStage_ReturnsOutgoingMeetingPayload()
        {
            // Arrange
            var jobVacancyId = Guid.NewGuid();
            var hrManagerId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, "Company");

            var responseDto = new FinalizeTop5ResponseDto
            {
                Top5PromotedCount = 5,
                RejectedCount = 12,
                JobVacancyId = jobVacancyId
            };

            _assessmentServiceMock.Setup(s => s.FinalizeTop5Async(jobVacancyId, hrManagerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseDto);

            // Act
            var result = await controller.FinalizeTop5(jobVacancyId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<FinalizeTop5ResponseDto>(okResult.Value);
            Assert.Equal(5, response.Top5PromotedCount);
        }

        [Fact]
        public async Task LogProctorEvent_TelemetryRecorded_ReturnsSuccess()
        {
            // Arrange
            var submissionId = Guid.NewGuid();
            var controller = CreateController(Guid.NewGuid(), "CANDIDATE");

            var eventDto = new ProctorEventRequestDto { EventType = "TabSwitch", Details = "Candidate switched tab" };
            _assessmentServiceMock.Setup(s => s.LogProctorEventAsync(submissionId, eventDto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await controller.LogProctorEvent(submissionId, eventDto, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task BlockAssessment_CandidateLeavesTab_BlocksRetake()
        {
            // Arrange
            var submissionId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();
            var controller = CreateController(candidateId, "CANDIDATE");

            _assessmentServiceMock.Setup(s => s.BlockAssessmentAsync(submissionId, candidateId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await controller.BlockAssessment(submissionId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
