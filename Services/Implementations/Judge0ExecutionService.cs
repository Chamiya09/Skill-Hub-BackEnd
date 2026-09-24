using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class Judge0ExecutionService : IPistonExecutionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Judge0ExecutionService> _logger;

        private const string DefaultJudge0Url = "http://127.0.0.1:2358";
        private const int DefaultExecutionTimeoutMs = 15000;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public Judge0ExecutionService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<Judge0ExecutionService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public (string runtimeLanguage, string version) ResolveRuntime(string language)
        {
            var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "python" or "py" or "python3" => ("python", "3.8.1"),
                "javascript" or "js" or "node" => ("javascript", "12.14.0"),
                "typescript" or "ts" => ("typescript", "3.7.4"),
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => ("csharp", "Mono 6.6.0.161"),
                "java" => ("java", "OpenJDK 13.0.1"),
                "cpp" or "c++" => ("c++", "GCC 9.2.0"),
                "go" or "golang" => ("go", "1.13.5"),
                _ => ("python", "3.8.1")
            };
        }

        public static int GetJudge0LanguageId(string runtimeLang)
        {
            return (runtimeLang ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "python" or "py" or "python3" => 71,
                "javascript" or "js" or "node" => 63,
                "typescript" or "ts" => 74,
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => 51,
                "java" => 62,
                "cpp" or "c++" => 54,
                "go" or "golang" => 60,
                _ => 71
            };
        }

        public async Task<PistonExecuteResult> ExecuteCodeAsync(
            string code,
            string language,
            string? stdin = null,
            CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString("N");
            var (runtimeLang, version) = ResolveRuntime(language);
            var langId = GetJudge0LanguageId(runtimeLang);

            // Preprocess and smartly wrap snippet code if bare statements were submitted
            var preprocessedCode = PreprocessCodeForJudge0(code, runtimeLang);

            var judge0Url = _configuration["ExecutionEngine:Judge0Url"];
            if (string.IsNullOrWhiteSpace(judge0Url))
            {
                judge0Url = DefaultJudge0Url;
            }
            judge0Url = judge0Url.TrimEnd('/');

            var submissionEndpoint = $"{judge0Url}/submissions?base64_encoded=false&wait=true";

            var payload = new Judge0SubmissionRequest
            {
                SourceCode = preprocessedCode,
                LanguageId = langId,
                Stdin = stdin ?? string.Empty,
                CpuTimeLimit = 5.0f,
                WallTimeLimit = 15.0f,
                MemoryLimit = 2048000
            };

            var jsonContent = JsonSerializer.Serialize(payload, JsonOpts);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, submissionEndpoint)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(DefaultExecutionTimeoutMs);

                var response = await _httpClient.SendAsync(request, timeoutCts.Token);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Judge0 returned 429 Too Many Requests for execution {Id}", executionId);
                    return new PistonExecuteResult
                    {
                        IsRateLimited = true,
                        IsError = true,
                        ErrorMessage = "Code execution service is busy. Please try again in a few moments.",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    stopwatch.Stop();
                    _logger.LogError("Judge0 returned HTTP {StatusCode}: {Body}", response.StatusCode, errorBody);
                    return new PistonExecuteResult
                    {
                        IsError = true,
                        ErrorMessage = $"Execution engine error (HTTP {(int)response.StatusCode}): {errorBody}",
                        Stderr = errorBody,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var submissionResult = JsonSerializer.Deserialize<Judge0SubmissionResponse>(responseContent, JsonOpts);

                stopwatch.Stop();

                return MapJudge0ResponseToPistonResult(submissionResult, stopwatch.ElapsedMilliseconds);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to connect to Judge0 execution engine at {Url}", judge0Url);
                return new PistonExecuteResult
                {
                    IsError = true,
                    ErrorMessage = $"Execution engine (Judge0) is not reachable at {judge0Url}. Please ensure Docker Desktop and the Judge0 containers are running.",
                    Stderr = "Docker / Judge0 server connection failed.",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogWarning("Judge0 submission timed out after {TimeoutMs}ms for execution {Id}", DefaultExecutionTimeoutMs, executionId);
                return new PistonExecuteResult
                {
                    IsError = true,
                    ErrorMessage = "Code execution timed out waiting for Judge0 response.",
                    Stderr = "Execution timed out.",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error executing code in Judge0 for {Language} (ID: {Id})", runtimeLang, executionId);
                return new PistonExecuteResult
                {
                    IsError = true,
                    ErrorMessage = $"Unexpected execution error: {ex.Message}",
                    Stderr = ex.Message,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private static string PreprocessCodeForJudge0(string code, string runtimeLang)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            var trimmed = code.Trim();

            switch (runtimeLang)
            {
                case "java":
                {
                    // Check if a class definition exists
                    bool hasClass = Regex.IsMatch(trimmed, @"\b(class|interface|enum)\b", RegexOptions.IgnoreCase);
                    if (!hasClass)
                    {
                        // Bare statements: wrap into public class Main
                        return $@"import java.util.*;
import java.io.*;
import java.math.*;

public class Main {{
    public static void main(String[] args) {{
{IndentCode(trimmed, 2)}
    }}
}}";
                    }

                    // Judge0 writes Java to Main.java. If code uses 'public class <OtherName>', normalize to 'public class Main'
                    return Regex.Replace(trimmed, @"public\s+class\s+[A-Za-z0-9_]+", "public class Main");
                }

                case "csharp":
                {
                    // Check if class/namespace exists
                    bool hasClass = Regex.IsMatch(trimmed, @"\b(class|struct|namespace)\b", RegexOptions.IgnoreCase);
                    if (!hasClass)
                    {
                        // Bare statements: wrap into class Program with Main
                        return $@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{{
    static void Main(string[] args)
    {{
{IndentCode(trimmed, 2)}
    }}
}}";
                    }
                    return trimmed;
                }

                case "c++":
                {
                    // Check if main() exists
                    bool hasMain = Regex.IsMatch(trimmed, @"\bmain\s*\(", RegexOptions.IgnoreCase);
                    if (!hasMain)
                    {
                        return $@"#include <iostream>
#include <vector>
#include <string>
#include <algorithm>
#include <cmath>

using namespace std;

int main() {{
{IndentCode(trimmed, 1)}
    return 0;
}}";
                    }
                    return trimmed;
                }

                default:
                    return trimmed;
            }
        }

        private static string IndentCode(string code, int indents)
        {
            var prefix = new string(' ', indents * 4);
            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            return string.Join(Environment.NewLine, lines.Select(l => prefix + l));
        }

        private static PistonExecuteResult MapJudge0ResponseToPistonResult(
            Judge0SubmissionResponse? response,
            long fallbackElapsedMs)
        {
            if (response == null)
            {
                return new PistonExecuteResult
                {
                    IsError = true,
                    ErrorMessage = "Null response received from execution engine."
                };
            }

            long elapsedMs = fallbackElapsedMs;
            if (!string.IsNullOrEmpty(response.Time) &&
                float.TryParse(response.Time, NumberStyles.Any, CultureInfo.InvariantCulture, out float timeSec))
            {
                elapsedMs = (long)(timeSec * 1000);
            }

            var statusId = response.Status?.Id ?? 0;
            var statusDesc = response.Status?.Description ?? "Unknown";

            // Status 3: Accepted
            if (statusId == 3)
            {
                return new PistonExecuteResult
                {
                    Stdout = response.Stdout ?? string.Empty,
                    Stderr = response.Stderr ?? string.Empty,
                    ExitCode = 0,
                    IsError = false,
                    ExecutionTimeMs = elapsedMs
                };
            }

            // Status 4: Wrong Answer (Only for internal Judge0 test cases; for our runner, stdout is captured)
            if (statusId == 4)
            {
                return new PistonExecuteResult
                {
                    Stdout = response.Stdout ?? string.Empty,
                    Stderr = response.Stderr ?? string.Empty,
                    ExitCode = 0,
                    IsError = false,
                    ExecutionTimeMs = elapsedMs
                };
            }

            // Status 5: Time Limit Exceeded
            if (statusId == 5)
            {
                return new PistonExecuteResult
                {
                    Stdout = response.Stdout ?? string.Empty,
                    Stderr = "Time limit exceeded (Execution timed out).",
                    ExitCode = 124,
                    IsError = true,
                    ErrorMessage = "Time limit exceeded (Execution timed out).",
                    ExecutionTimeMs = elapsedMs
                };
            }

            // Status 6: Compilation Error
            if (statusId == 6)
            {
                var compileOutput = response.CompileOutput ?? response.Stderr ?? "Compilation failed.";
                return new PistonExecuteResult
                {
                    CompileOutput = compileOutput,
                    Stderr = compileOutput,
                    ExitCode = 1,
                    IsError = true,
                    ErrorMessage = "Compilation Error",
                    ExecutionTimeMs = elapsedMs
                };
            }

            // Status 7..12: Runtime Errors (NZEC, SIGSEGV, SIGXFSZ, etc.)
            var errText = !string.IsNullOrWhiteSpace(response.Stderr)
                ? response.Stderr
                : (!string.IsNullOrWhiteSpace(response.Message) ? response.Message : statusDesc);

            return new PistonExecuteResult
            {
                Stdout = response.Stdout ?? string.Empty,
                Stderr = errText,
                ExitCode = response.ExitCode ?? 1,
                IsError = true,
                ErrorMessage = response.Message ?? statusDesc,
                ExecutionTimeMs = elapsedMs
            };
        }

        private sealed class Judge0SubmissionRequest
        {
            [JsonPropertyName("source_code")]
            public string SourceCode { get; set; } = string.Empty;

            [JsonPropertyName("language_id")]
            public int LanguageId { get; set; }

            [JsonPropertyName("stdin")]
            public string Stdin { get; set; } = string.Empty;

            [JsonPropertyName("cpu_time_limit")]
            public float CpuTimeLimit { get; set; } = 5.0f;

            [JsonPropertyName("wall_time_limit")]
            public float WallTimeLimit { get; set; } = 15.0f;

            [JsonPropertyName("memory_limit")]
            public int MemoryLimit { get; set; } = 2048000;
        }

        private sealed class Judge0SubmissionResponse
        {
            [JsonPropertyName("stdout")]
            public string? Stdout { get; set; }

            [JsonPropertyName("stderr")]
            public string? Stderr { get; set; }

            [JsonPropertyName("compile_output")]
            public string? CompileOutput { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("exit_code")]
            public int? ExitCode { get; set; }

            [JsonPropertyName("time")]
            public string? Time { get; set; }

            [JsonPropertyName("memory")]
            public long? Memory { get; set; }

            [JsonPropertyName("token")]
            public string? Token { get; set; }

            [JsonPropertyName("status")]
            public Judge0Status? Status { get; set; }
        }

        private sealed class Judge0Status
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;
        }
    }
}

