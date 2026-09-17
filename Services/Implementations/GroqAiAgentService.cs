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

            var apiKey = _configuration["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("GROQ_API_KEY is not configured.");

            var model = _configuration["Groq:Model"] ?? "openai/gpt-oss-20b";
            var fallbackModel = _configuration["Groq:FallbackModel"]
                ?? Environment.GetEnvironmentVariable("GROQ_FALLBACK_MODEL")
                ?? "qwen/qwen3.8-27b";

            var candidateJson = JsonSerializer.Serialize(request.Candidate, JsonOptions);
            var jobJson = JsonSerializer.Serialize(request.Job, JsonOptions);

            var systemPrompt = """
                You are a rigorous, evidence-only candidate-to-job evaluator. Analyze every
                available section of the Candidate JSON against the Job JSON: technical skills,
                work experience, seniority and titles, featured projects and outcomes, education,
                certifications, headline, and executive summary. Use only facts present in those
                two JSON objects. Never invent, assume, or hallucinate skills, accomplishments,
                requirements, qualifications, dates, or experience.

                Calculate MatchPercentage as the integer sum of these five component scores:
                1. Technical Skills & Core Stack: 0-30 points. Strictly compare required and
                   preferred technologies with the candidate's skills and demonstrated usage.
                2. Work Experience & Seniority: 0-25 points. Compare required years, relevant
                   duration, responsibilities, domain experience, and job-title/seniority alignment.
                3. Featured Projects & Outcomes: 0-20 points. Award points only for project evidence
                   showing practical use of relevant technologies, responsibilities, and outcomes.
                4. Education & Certifications: 0-15 points. Evaluate relevant verified degrees,
                   fields of study, accredited certifications, and badges against job requirements.
                5. Executive Summary / Culture Fit: 0-10 points. Compare the summary and headline
                   with the role's mission, responsibilities, domain, collaboration, and values.

                Give high component scores for complete, directly evidenced alignment. Deduct
                points strictly and proportionally for every missing, mismatched, weakly evidenced,
                or below-threshold job requirement. Do not award points merely because a section
                exists. Treat absent candidate sections as no evidence for that component. Treat
                absent job requirements gracefully: do not invent requirements; evaluate only the
                available criteria and explain the limited evidence. Recognize clear semantic
                equivalents, but do not treat merely adjacent technologies as exact matches.

                Strengths must contain specific, analytical evidence tied to the job. MissingSkillGaps
                must contain only genuine missing or insufficiently evidenced job requirements.
                AiRecommendation must be a detailed but concise hiring recommendation that reports
                the five component scores and explains the most important evidence and deductions.

                Return ONLY one valid JSON object, without markdown or commentary, with exactly this
                schema and no additional keys:
                {"MatchPercentage":0,"Strengths":[],"MissingSkillGaps":[],"AiRecommendation":""}
                MatchPercentage must be an integer from 0 to 100 and equal the five component scores.
                Strengths and MissingSkillGaps must be JSON arrays of strings. All fields are required.
                """;

            var userPrompt = $"""
                Candidate JSON:
                {candidateJson}

                Job JSON:
                {jobJson}
                """;

            var messages = new List<GroqMessage>
            {
                new("system", systemPrompt),
                new("user", userPrompt),
            };

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var response = await SendAsync(client, apiKey, model, messages, cancellationToken);
                try
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests &&
                        !string.Equals(model, fallbackModel, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Groq model {PrimaryModel} is rate limited; retrying once with {FallbackModel}.",
                            model,
                            fallbackModel);
                        response.Dispose();
                        response = await SendAsync(
                            client, apiKey, fallbackModel, messages, cancellationToken);
                        responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    }

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
                finally
                {
                    response.Dispose();
                }
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

        private static async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            string apiKey,
            string model,
            List<GroqMessage> messages,
            CancellationToken cancellationToken)
        {
            var payload = new GroqChatRequest(
                model,
                messages,
                new GroqResponseFormat("json_object"),
                Temperature: 0);
            Console.WriteLine($"Sending dynamic match data to Groq: {JsonSerializer.Serialize(payload, JsonOptions)}");

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return await client.SendAsync(request, cancellationToken);
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
