using System.Text.Json.Serialization;
using Skill_Hub_BackEnd.DTOs.Assessments;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    /// <summary>
    /// Typed HTTP client that communicates with the Python AI Assessment Agent microservice
    /// (FastAPI on port 8000) to generate calibrated coding assessments.
    /// Consumes: POST {AiAgent:BaseUrl}/api/assessment-agent/generate-question
    /// </summary>
    public interface IPythonAssessmentAgentClient
    {
        Task<PythonGenerateQuestionResponse> GenerateQuestionAsync(
            PythonGenerateQuestionRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class PythonGenerateQuestionRequest
    {
        [JsonPropertyName("job_vacancy_id")]
        public string JobVacancyId { get; set; } = string.Empty;

        [JsonPropertyName("focus_area")]
        public string? FocusArea { get; set; }

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; } = "Medium";

        [JsonPropertyName("job_context")]
        public PythonJobVacancyContext? JobContext { get; set; }
    }

    public sealed class PythonJobVacancyContext
    {
        [JsonPropertyName("job_title")]
        public string JobTitle { get; set; } = string.Empty;

        [JsonPropertyName("experience_level")]
        public string ExperienceLevel { get; set; } = string.Empty;

        [JsonPropertyName("department")]
        public string Department { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public sealed class PythonGenerateQuestionResponse
    {
        [JsonPropertyName("job_vacancy_id")]
        public string JobVacancyId { get; set; } = string.Empty;

        [JsonPropertyName("job_title")]
        public string JobTitle { get; set; } = string.Empty;

        [JsonPropertyName("experience_level")]
        public string ExperienceLevel { get; set; } = string.Empty;

        [JsonPropertyName("selected_language")]
        public string SelectedLanguage { get; set; } = string.Empty;

        [JsonPropertyName("question")]
        public CodingQuestionItemDto Question { get; set; } = new();
    }
}