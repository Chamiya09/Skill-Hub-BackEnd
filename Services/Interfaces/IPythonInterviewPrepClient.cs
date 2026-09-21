using System.Text.Json.Serialization;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    /// <summary>
    /// Typed HTTP client that calls the Python AI Agent microservice (FastAPI on port 8000)
    /// to generate technical study guidelines for the candidate's specific job description.
    /// Consumes: POST {AiAgent:BaseUrl}/api/student1/generate-guide
    /// </summary>
    public interface IPythonInterviewPrepClient
    {
        Task<PythonInterviewPrepResponse> GenerateGuideAsync(
            PythonGenerateGuideRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class PythonGenerateGuideRequest
    {
        [JsonPropertyName("job_title")]
        public string JobTitle { get; set; } = string.Empty;

        [JsonPropertyName("target_role")]
        public string? TargetRole { get; set; }

        [JsonPropertyName("job_description")]
        public string JobDescription { get; set; } = string.Empty;

        [JsonPropertyName("experience_level")]
        public string ExperienceLevel { get; set; } = "Mid-Senior";

        [JsonPropertyName("company_name")]
        public string CompanyName { get; set; } = "Enterprise Partner";
    }

    public sealed class PythonInterviewPrepResponse
    {
        [JsonPropertyName("role_overview_summary")]
        public string RoleOverviewSummary { get; set; } = string.Empty;

        [JsonPropertyName("key_theoretical_areas")]
        public List<PythonStudyFocusArea> KeyTheoreticalAreas { get; set; } = new();

        [JsonPropertyName("practical_implementation_focus")]
        public List<PythonStudyFocusArea> PracticalImplementationFocus { get; set; } = new();

        [JsonPropertyName("pro_tips")]
        public List<string> ProTips { get; set; } = new();

        [JsonPropertyName("preparation_checklist")]
        public List<string> PreparationChecklist { get; set; } = new();
    }

    public sealed class PythonStudyFocusArea
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("section")]
        public string Section { get; set; } = "Key Theoretical Areas";

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "High Priority";

        [JsonPropertyName("estimated_study_time")]
        public string EstimatedStudyTime { get; set; } = "35-45 mins";

        [JsonPropertyName("overview")]
        public string Overview { get; set; } = string.Empty;

        [JsonPropertyName("concepts_to_review")]
        public List<string> ConceptsToReview { get; set; } = new();

        [JsonPropertyName("practical_application")]
        public string PracticalApplication { get; set; } = string.Empty;

        [JsonPropertyName("coach_tip")]
        public string CoachTip { get; set; } = string.Empty;
    }
}
