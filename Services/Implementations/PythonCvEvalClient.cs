using System.Net.Http.Json;
using System.Text.Json;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class PythonCvEvalClient : IPythonCvEvalClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PythonCvEvalClient> _logger;

        public PythonCvEvalClient(HttpClient httpClient, ILogger<PythonCvEvalClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<PythonCvEvalResponse> EvaluateAsync(
            string cvText,
            string jobDescription,
            string requiredSkills,
            CancellationToken cancellationToken = default)
        {
            // Note: Required Skills are explicitly sent alongside the jobDescription
            var payload = new
            {
                cv_text = cvText,
                job_description = jobDescription,
                required_skills = requiredSkills
            };

            _logger.LogInformation("[PythonCvEvalClient] Calling POST /api/student2/evaluate-cv on Python microservice...");

            var response = await _httpClient.PostAsJsonAsync(
                "api/student2/evaluate-cv",
                payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Deserializes snake_case Python properties directly to C# PascalCase properties
            var result = await response.Content.ReadFromJsonAsync<PythonCvEvalResponse>(
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
                cancellationToken);

            if (result is null)
                throw new InvalidOperationException("Python service returned empty response for CV evaluation.");

            _logger.LogInformation("[PythonCvEvalClient] Received evaluation. Score={Score}", result.MatchScore);
            return result;
        }
    }
}
