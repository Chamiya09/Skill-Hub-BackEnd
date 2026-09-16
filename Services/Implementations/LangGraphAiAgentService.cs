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

        public async Task<AiMatchResponseDto> AnalyzeCandidateMatchAsync(
            AiMatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Forward only the DTOs bound from this request; no sample or fallback
            // candidate/job data is introduced in the service layer.
            var payload = new LangGraphMatchRequest(request.Candidate, request.Job);

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.PostAsJsonAsync(
                    "api/ai/analyze-match",
                    payload,
                    JsonOptions,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "LangGraph match endpoint returned {StatusCode}: {ResponseBody}",
                        (int)response.StatusCode,
                        responseBody);
                    response.EnsureSuccessStatusCode();
                }

                var result = await response.Content.ReadFromJsonAsync<LangGraphMatchResponse>(
                    JsonOptions,
                    cancellationToken);
                if (result is null ||
                    result.MatchPercentage is < 0 or > 100 ||
                    string.IsNullOrWhiteSpace(result.Recommendation))
                {
                    throw new JsonException("The LangGraph service returned an invalid response.");
                }

                return new AiMatchResponseDto
                {
                    MatchPercentage = result.MatchPercentage,
                    Strengths = result.Strengths ?? new List<string>(),
                    MissingSkillGaps = result.MissingSkills ?? new List<string>(),
                    AiRecommendation = result.Recommendation
                };
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "Unable to communicate with the LangGraph AI service.");
                throw new InvalidOperationException(
                    "The AI match-analysis service is unavailable.", exception);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
            [property: JsonPropertyName("candidate_data")] CandidateDto Candidate,
            [property: JsonPropertyName("job_data")] JobDto Job);

        private sealed record LangGraphMatchResponse(
            [property: JsonPropertyName("MatchPercentage")] int MatchPercentage,
            [property: JsonPropertyName("Strengths")] List<string>? Strengths,
            [property: JsonPropertyName("MissingSkillGaps")] List<string>? MissingSkills,
            [property: JsonPropertyName("AiRecommendation")] string Recommendation);
    }
}
