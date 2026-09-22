using System.Net.Http.Json;
using System.Text.Json;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class PythonAssessmentAgentClient : IPythonAssessmentAgentClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PythonAssessmentAgentClient> _logger;

        public PythonAssessmentAgentClient(
            HttpClient httpClient,
            ILogger<PythonAssessmentAgentClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<PythonGenerateQuestionResponse> GenerateQuestionAsync(
            PythonGenerateQuestionRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[PythonAssessmentAgentClient] Calling POST api/assessment-agent/generate-question for job: {JobId}...",
                request.JobVacancyId);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/assessment-agent/generate-question",
                request,
                options,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "[PythonAssessmentAgentClient] Call to Python service failed with status {StatusCode}: {ErrorBody}",
                    response.StatusCode, errorBody);

                throw new HttpRequestException(
                    $"Python AI service returned status {(int)response.StatusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<PythonGenerateQuestionResponse>(options, cancellationToken);
            if (result is null || result.Question is null)
            {
                throw new InvalidOperationException("Python AI service returned null response for assessment challenge.");
            }

            _logger.LogInformation(
                "[PythonAssessmentAgentClient] Successfully received challenge '{Title}' ({Language}) for '{JobTitle}'.",
                result.Question.Title, result.SelectedLanguage, result.JobTitle);

            return result;
        }
    }
}

