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
        public DateTime StartedAt { get; set; }
        public List<CandidateCodingQuestionDto> Questions { get; set; } = new();
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
    }

    public sealed class SubmitAnswersRequestDto
    {
        [Required]
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
    }
}

