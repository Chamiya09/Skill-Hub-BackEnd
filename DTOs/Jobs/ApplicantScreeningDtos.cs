namespace Skill_Hub_BackEnd.DTOs.Jobs
{
    /// <summary>
    /// Per-category score breakdown returned by the Python LangGraph microservice.
    /// The weights sum to 100 (Skills 30 + Experience 25 + Projects 20 + Education 15 + Certifications 10).
    /// </summary>
    public sealed class ScoreBreakdown
    {
        public int Skills { get; set; }
        public int Experience { get; set; }
        public int Projects { get; set; }
        public int Education { get; set; }
        public int Certifications { get; set; }
    }

    public sealed class ScreenedApplicantDto
    {
        public Guid ApplicationId { get; set; }
        public Guid CandidateId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Headline { get; set; }
        public List<string> Skills { get; set; } = new();
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; } = "Applied";
        public int? AiMatchScore { get; set; }

        /// <summary>
        /// Breakdown of the AI match score by category. Null when the candidate has not been screened yet.
        /// </summary>
        public ScoreBreakdown? ScoreBreakdown { get; set; }
    }

    /// <summary>
    /// Returned by GET /api/jobs/{jobId}/shortlisted.
    /// Contains the rich profile data required by the Hiring Pipeline board.
    /// </summary>
    public sealed class ShortlistedApplicantDto
    {
        public Guid ApplicationId { get; set; }
        public Guid CandidateId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Headline { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public List<string> Skills { get; set; } = new();
        public DateTime AppliedDate { get; set; }
        public DateTime? ShortlistedAt { get; set; }
        public int? AiMatchScore { get; set; }
    }
}
