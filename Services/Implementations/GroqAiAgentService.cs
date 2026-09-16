using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skill_Hub_BackEnd.DTOs.Ai;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class GroqAiAgentService : IAiAgentService
    {
        public const string HttpClientName = "GroqAiAgent";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroqAiAgentService> _logger;

        public GroqAiAgentService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GroqAiAgentService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AiMatchResponseDto> AnalyzeCandidateMatchAsync(
            AiMatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var apiKey = _configuration["Groq:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
                ?? throw new InvalidOperationException("GROQ_API_KEY is not configured.");
            var model = _configuration["Groq:Model"] ?? "openai/gpt-oss-20b";

            var candidateJson = JsonSerializer.Serialize(request.Candidate, JsonOptions);
            var jobJson = JsonSerializer.Serialize(request.Job, JsonOptions);

            var systemPrompt = """
                You are an evidence-only candidate-to-job matching engine.
                Evaluate only facts explicitly present in the supplied Candidate and Job JSON.
                Never invent skills, requirements, qualifications, or experience.
                Score Tech Stack Match from 0-50, Experience Match from 0-30, and
                Architecture/Concept Match from 0-20. MatchPercentage is their sum.
                Empty skills or requirements are valid inputs and must not cause an error.
                Return ONLY one JSON object with exactly this schema and no other keys:
                {"MatchPercentage":0,"Strengths":[],"MissingSkillGaps":[],"AiRecommendation":""}
                MatchPercentage must be an integer from 0 to 100. All other fields are required.
                """;

            var userPrompt = $"""
                Candidate JSON:
                {candidateJson}

                Job JSON:
                {jobJson}
                """;

            var groqRequest = new GroqChatRequest(
                model,
                new List<GroqMessage>
                {
                    new("system", systemPrompt),
                    new("user", userPrompt),
                },
                new GroqResponseFormat("json_object"),
                Temperature: 0);

            var outgoingJson = JsonSerializer.Serialize(groqRequest, JsonOptions);
            Console.WriteLine($"Sending dynamic match data to Groq: {outgoingJson}");

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(groqRequest, options: JsonOptions),
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var response = await client.SendAsync(httpRequest, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Groq returned {StatusCode}: {ResponseBody}",
                        (int)response.StatusCode, responseBody);
                    response.EnsureSuccessStatusCode();
                }

                var envelope = JsonSerializer.Deserialize<GroqChatResponse>(responseBody, JsonOptions);
                var content = envelope?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                    throw new JsonException("Groq returned an empty completion.");

                var result = JsonSerializer.Deserialize<GroqMatchResult>(content, JsonOptions);
                if (result is null || result.MatchPercentage is < 0 or > 100 ||
                    string.IsNullOrWhiteSpace(result.AiRecommendation))
                    throw new JsonException("Groq returned an invalid match result.");

                return new AiMatchResponseDto
                {
                    MatchPercentage = result.MatchPercentage,
                    Strengths = result.Strengths ?? new(),
                    MissingSkillGaps = result.MissingSkillGaps ?? new(),
                    AiRecommendation = result.AiRecommendation,
                };
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
            {
                _logger.LogError(exception, "Direct Groq match analysis failed.");
                throw new InvalidOperationException("The Groq match-analysis service is unavailable.", exception);
            }
        }

        private sealed record GroqChatRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("messages")] List<GroqMessage> Messages,
            [property: JsonPropertyName("response_format")] GroqResponseFormat ResponseFormat,
            [property: JsonPropertyName("temperature")] decimal Temperature);
        private sealed record GroqMessage(string Role, string Content);
        private sealed record GroqResponseFormat(string Type);
        private sealed record GroqChatResponse(List<GroqChoice>? Choices);
        private sealed record GroqChoice(GroqResponseMessage? Message);
        private sealed record GroqResponseMessage(string? Content);
        private sealed record GroqMatchResult(
            int MatchPercentage,
            List<string>? Strengths,
            List<string>? MissingSkillGaps,
            string AiRecommendation);
    }
}
