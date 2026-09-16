using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skill_Hub_BackEnd.DTOs.Ai;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class LangGraphAiAgentService : IAiAgentService
    {
        public const string HttpClientName = "LangGraphAiAgent";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LangGraphAiAgentService> _logger;

        public LangGraphAiAgentService(
            IHttpClientFactory httpClientFactory,
            ILogger<LangGraphAiAgentService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AiMatchResponseDto> AnalyzeCandidateMatchAsync(AiMatchRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var payload = new LangGraphMatchRequest(
                request.CandidateSkills,
                request.CandidateExperienceYears,
                request.JobRequirements);

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.PostAsJsonAsync(
                    "api/ai/analyze-match",
                    payload,
                    JsonOptions);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<LangGraphMatchResponse>(JsonOptions);
                if (result is null ||
                    result.Breakdown is null ||
                    result.Breakdown.Skills is < 0 or > 40 ||
                    result.Breakdown.Experience is < 0 or > 35 ||
                    result.Breakdown.Projects is < 0 or > 25 ||
                    result.MatchPercentage is < 0 or > 100 ||
                    result.MatchPercentage != result.Breakdown.Skills
                        + result.Breakdown.Experience
                        + result.Breakdown.Projects ||
                    string.IsNullOrWhiteSpace(result.Recommendation))
                {
                    throw new JsonException("The LangGraph service returned an invalid response.");
                }

                return new AiMatchResponseDto
                {
                    MatchPercentage = result.MatchPercentage,
                    Strengths = result.Strengths ?? new List<string>(),
                    MissingSkills = result.MissingSkills ?? new List<string>(),
                    AiRecommendation = result.Recommendation
                };
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "Unable to communicate with the LangGraph AI service.");
                throw new InvalidOperationException(
                    "The AI match-analysis service is unavailable.", exception);
            }
            catch (TaskCanceledException exception)
            {
                _logger.LogError(exception, "The LangGraph AI service request timed out.");
                throw new InvalidOperationException(
                    "The AI match-analysis service timed out.", exception);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "The LangGraph AI service returned invalid JSON.");
                throw new InvalidOperationException(
                    "The AI match-analysis service returned an invalid response.", exception);
            }
        }

        private sealed record LangGraphMatchRequest(
            [property: JsonPropertyName("candidate_skills")] List<string> CandidateSkills,
            [property: JsonPropertyName("candidate_experience_years")] int CandidateExperienceYears,
            [property: JsonPropertyName("job_requirements")] List<string> JobRequirements);

        private sealed record LangGraphMatchResponse(
            [property: JsonPropertyName("breakdown")] LangGraphMatchBreakdown Breakdown,
            [property: JsonPropertyName("matchPercentage")] int MatchPercentage,
            [property: JsonPropertyName("strengths")] List<string>? Strengths,
            [property: JsonPropertyName("missingSkills")] List<string>? MissingSkills,
            [property: JsonPropertyName("aiRecommendation")] string Recommendation);

        private sealed record LangGraphMatchBreakdown(
            [property: JsonPropertyName("skills")] int Skills,
            [property: JsonPropertyName("experience")] int Experience,
            [property: JsonPropertyName("projects")] int Projects);
    }
}
