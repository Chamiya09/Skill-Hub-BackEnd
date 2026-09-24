using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Assessments
{
    public sealed class CreateAssessmentManualDto
    {
        [Required]
        public Guid JobVacancyId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal PassingThreshold { get; set; } = 60.00m;

        [Range(1, 300)]
        public int TimeLimitMinutes { get; set; } = 60;

        [Required]
        [MinLength(1, ErrorMessage = "At least one coding question is required.")]
        public List<CodingQuestionItemDto> Questions { get; set; } = new();

        public bool PublishImmediately { get; set; } = true;

        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class UpdateAssessmentDto
    {
        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal PassingThreshold { get; set; } = 60.00m;

        [Range(1, 300)]
        public int TimeLimitMinutes { get; set; } = 60;

        [Required]
        public List<CodingQuestionItemDto> FinalQuestions { get; set; } = new();

        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class AssessmentResponseDto
    {
        public Guid Id { get; set; }

        public Guid JobVacancyId { get; set; }

        public string Title { get; set; } = string.Empty;

        public List<CodingQuestionItemDto> GeneratedQuestions { get; set; } = new();

        public List<CodingQuestionItemDto> FinalQuestions { get; set; } = new();

        public decimal PassingThreshold { get; set; }

        public int TimeLimitMinutes { get; set; }

        public Guid CreatedBy { get; set; }

        public string Status { get; set; } = "Draft";

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public int TotalSubmissions { get; set; }
        public bool HasActiveCandidateExam { get; set; }
        public bool HasSuspendedCandidateExam { get; set; }
        public bool CanEdit { get; set; } = true;
    }

    public sealed class AssessmentTrackSummaryDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int TimeLimitMinutes { get; set; }

        public int QuestionCount { get; set; }

        public decimal PassingThreshold { get; set; }

        public string Status { get; set; } = "Published";

        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class GenerateAiAssessmentRequestDto
    {
        public string? FocusArea { get; set; }
        public string? Difficulty { get; set; } = "Medium";
    }

    public sealed class JobVacancyContextDto
    {
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

