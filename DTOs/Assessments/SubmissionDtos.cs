using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Skill_Hub_BackEnd.DTOs.Assessments
{
    /// <summary>
    /// Incoming contract from Student 2 (ATS) / HR Dispatch modal.
    /// </summary>
    public sealed class DispatchAssessmentRequestDto
    {
        [Required]
        public Guid AssessmentId { get; set; }

        [Required]
        public Guid CandidateId { get; set; }

        [Required]
        public Guid ApplicationId { get; set; }

        [Required]
        public Guid JobVacancyId { get; set; }

        [Range(0, 100)]
        public decimal CvMatchScore { get; set; } = 0.00m;

        public int ExpiresInHours { get; set; } = 48;
    }

    public sealed class DispatchAssessmentResponseDto
    {
        public Guid SubmissionId { get; set; }
        public Guid AssessmentId { get; set; }
        public string AssessmentTitle { get; set; } = string.Empty;
        public Guid CandidateId { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string TestLink { get; set; } = string.Empty;
        public int ExpiresInHours { get; set; } = 48;
        public string Status { get; set; } = "Assigned";
        public string Message { get; set; } = string.Empty;
    }

    public sealed class StartExamResponseDto
    {
        public Guid SubmissionId { get; set; }
        public Guid AssessmentId { get; set; }
        public string AssessmentTitle { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
        public DateTime? StartedAt { get; set; }
        public string Status { get; set; } = "Assigned";
        public int? RemainingSeconds { get; set; }
        public List<SubmittedAnswerItemDto>? DraftAnswers { get; set; }
        public List<CandidateCodingQuestionDto> Questions { get; set; } = new();
    }

    public sealed class TestCaseEvaluationItemDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("expectedOutput")]
        public string ExpectedOutput { get; set; } = string.Empty;

        [JsonPropertyName("actualOutput")]
        public string ActualOutput { get; set; } = string.Empty;

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("isHidden")]
        public bool IsHidden { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }

    public sealed class SubmittedAnswerItemDto
    {
        [JsonPropertyName("questionId")]
        public string QuestionId { get; set; } = string.Empty;

        [JsonPropertyName("submittedCode")]
        public string SubmittedCode { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = "csharp";

        [JsonPropertyName("testCasesPassed")]
        public int TestCasesPassed { get; set; }

        [JsonPropertyName("totalTestCases")]
        public int TotalTestCases { get; set; }

        [JsonPropertyName("score")]
        public decimal Score { get; set; }

        [JsonPropertyName("testCaseResults")]
        public List<TestCaseEvaluationItemDto> TestCaseResults { get; set; } = new();
    }

    public sealed class RunCodeRequestDto
    {
        [Required]
        public string QuestionId { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;

        public string Language { get; set; } = "python";

        public string? CustomInput { get; set; }
    }

    public sealed class RunCodeResponseDto
    {
        public string Stdout { get; set; } = string.Empty;
        public string Stderr { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public string? CompileOutput { get; set; }
        public bool IsRateLimited { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public long ExecutionTimeMs { get; set; }
        public string? SampleInputUsed { get; set; }
        public string? ExpectedOutput { get; set; }
        public bool? SamplePassed { get; set; }
    }

    public sealed class SubmitAnswersRequestDto
    {
        [Required]
        public List<SubmittedAnswerItemDto> Answers { get; set; } = new();
    }

    public sealed class SaveDraftAnswersRequestDto
    {
        [JsonPropertyName("remainingSeconds")]
        public int? RemainingSeconds { get; set; }

        [JsonPropertyName("answers")]
        public List<SubmittedAnswerItemDto> Answers { get; set; } = new();
    }

    public sealed class ProctorEventRequestDto
    {
        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = "TAB_SWITCH";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }

    public sealed class ProctorSummaryDto
    {
        [JsonPropertyName("tabSwitches")]
        public int TabSwitches { get; set; }

        [JsonPropertyName("windowBlurs")]
        public int WindowBlurs { get; set; }

        [JsonPropertyName("remainingSeconds")]
        public int? RemainingSeconds { get; set; }

        [JsonPropertyName("events")]
        public List<ProctorEventRequestDto> Events { get; set; } = new();
    }

    public sealed class SubmissionDetailDto
    {
        public Guid Id { get; set; }
        public Guid AssessmentId { get; set; }
        public string AssessmentTitle { get; set; } = string.Empty;
        public Guid CandidateId { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        public Guid ApplicationId { get; set; }
        public Guid JobVacancyId { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public decimal ExamScore { get; set; }
        public decimal CvScore { get; set; }
        public decimal FinalWeightedScore { get; set; }
        public decimal PassingThreshold { get; set; }
        public string Status { get; set; } = "Assigned";
        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? GradedAt { get; set; }
        public List<SubmittedAnswerItemDto> Answers { get; set; } = new();
        public ProctorSummaryDto ProctorSummary { get; set; } = new();
        public bool IsSelectedForInterview { get; set; } = false;
        public string? ReviewerFeedback { get; set; }
        public Guid? ScheduledEventId { get; set; }
        public string? ScheduledDate { get; set; }
        public string? ScheduledTime { get; set; }
        public string? ScheduledMeetingMode { get; set; }
        public string? ScheduledLocation { get; set; }
    }

    public sealed class CandidateAssessmentListItemDto
    {
        public Guid SubmissionId { get; set; }
        public Guid AssessmentId { get; set; }
        public string AssessmentTitle { get; set; } = string.Empty;
        public Guid JobVacancyId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int TimeLimitMinutes { get; set; }
        public int QuestionCount { get; set; }
        public decimal PassingThreshold { get; set; }
        public string Status { get; set; } = "Assigned";
        public decimal ExamScore { get; set; }
        public decimal FinalWeightedScore { get; set; }
        public bool IsPassed { get; set; }
        public bool IsSelectedForInterview { get; set; } = false;
        public string? ReviewerFeedback { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public bool IsBlocked { get; set; }
    }

    public sealed class QuestionReviewItemDto
    {
        public string QuestionId { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public decimal PointsEarned { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ManualReviewSubmissionDto
    {
        public decimal ExamScore { get; set; }
        public bool IsSelectedForInterview { get; set; }
        public string? ReviewerFeedback { get; set; }
        public List<QuestionReviewItemDto> QuestionReviews { get; set; } = new();
    }
}

