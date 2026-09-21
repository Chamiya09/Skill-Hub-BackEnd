using System.Net.Http.Json;
using System.Text.Json;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class PythonInterviewPrepClient : IPythonInterviewPrepClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PythonInterviewPrepClient> _logger;

        public PythonInterviewPrepClient(
            HttpClient httpClient,
            ILogger<PythonInterviewPrepClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<PythonInterviewPrepResponse> GenerateGuideAsync(
            PythonGenerateGuideRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[PythonInterviewPrepClient] Calling POST api/student1/generate-guide for role: '{Role}' at '{Company}'...",
                request.JobTitle, request.CompanyName);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/student1/generate-guide",
                request,
                options,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "[PythonInterviewPrepClient] Call to Python service failed with status {StatusCode}: {ErrorBody}",
                    response.StatusCode, errorBody);

                throw new HttpRequestException(
                    $"Python AI service returned status {(int)response.StatusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<PythonInterviewPrepResponse>(options, cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException("Python AI service returned null response for interview prep guide.");
            }

            _logger.LogInformation(
                "[PythonInterviewPrepClient] Successfully received study guide. Theory: {TheoryCount}, Practical: {PracticalCount}, ProTips: {TipCount}",
                result.KeyTheoreticalAreas.Count,
                result.PracticalImplementationFocus.Count,
                result.ProTips.Count);

            return result;
        }
    }
}
