using System.Text.Json.Serialization;

namespace Skill_Hub_BackEnd.DTOs.Assessments
{
    public sealed class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public Guid SubmissionId { get; set; }
        public Guid ApplicationId { get; set; }
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public decimal CvScore { get; set; }
        public decimal ExamScore { get; set; }
        public decimal FinalWeightedScore { get; set; }
        public string SubmissionStatus { get; set; } = string.Empty;
        public string ApplicationStatus { get; set; } = string.Empty;
        public int ProctorTabSwitches { get; set; }
        public bool IsTop5 { get; set; }
        public bool IsPassed { get; set; }
        public bool IsSelectedForInterview { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    /// <summary>
    /// Outgoing Contract strictly matching the payload specification for Student 3 (Meeting Orchestration).
    /// </summary>
    public sealed class Student3OutgoingCandidateDto
    {
        [JsonPropertyName("applicationId")]
        public Guid ApplicationId { get; set; }

        [JsonPropertyName("candidateId")]
        public Guid CandidateId { get; set; }

        [JsonPropertyName("jobVacancyId")]
        public Guid JobVacancyId { get; set; }

        [JsonPropertyName("finalWeightedScore")]
        public decimal FinalWeightedScore { get; set; }

        [JsonPropertyName("hrManagerId")]
        public Guid HrManagerId { get; set; }
    }

    public sealed class FinalizeTop5ResponseDto
    {
        public Guid JobVacancyId { get; set; }
        public int TotalSubmissions { get; set; }
        public int PassedCount { get; set; }
        public int Top5PromotedCount { get; set; }
        public int RejectedCount { get; set; }
        public List<Student3OutgoingCandidateDto> OutgoingTop5Payload { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}

