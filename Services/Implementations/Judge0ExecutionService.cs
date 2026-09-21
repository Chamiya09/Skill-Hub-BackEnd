using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        private const string DefaultJudge0BaseUrl = "http://127.0.0.1:2358";
        private const int DefaultExecutionTimeoutSeconds = 15;

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
                "python" or "py" or "python3" => ("python", "3.10"),
                "javascript" or "js" or "node" => ("javascript", "18"),
                "typescript" or "ts" => ("typescript", "5"),
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => ("csharp", "Mono"),
                "java" => ("java", "OpenJDK 13"),
                "cpp" or "c++" => ("c++", "GCC 9.2"),
                "go" or "golang" => ("go", "1.13"),
                _ => ("python", "3.10")
            };
        }

        public static int MapLanguageToJudge0Id(string language)
        {
            var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "python" or "py" or "python3" => 71,       // Python (3.8.1) / Python 3
                "javascript" or "js" or "node" or "nodejs" => 63,     // JavaScript (Node.js 12.14.0)
                "typescript" or "ts" => 74,               // TypeScript (3.7.4)
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => 51,// C# (Mono 6.6.0.161)
                "java" => 62,                             // Java (OpenJDK 13.0.1)
                "cpp" or "c++" => 54,                     // C++ (GCC 9.2.0)
                "c" => 50,                               // C (GCC 9.2.0)
                "go" or "golang" => 60,                   // Go (1.13.5)
                "rust" => 73,                             // Rust (1.40.0)
                "ruby" => 72,                             // Ruby (2.7.0)
                "php" => 68,                              // PHP (7.4.1)
                _ => 71                                   // Default to Python 3
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
            var languageId = MapLanguageToJudge0Id(language);

            var baseUrl = _configuration["ExecutionEngine:Judge0:BaseUrl"] ?? DefaultJudge0BaseUrl;
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            var apiKey = _configuration["ExecutionEngine:Judge0:ApiKey"];
            var apiHost = _configuration["ExecutionEngine:Judge0:ApiHost"];
            bool useBase64 = _configuration.GetValue<bool>("ExecutionEngine:Judge0:UseBase64", false);

            var submissionEndpoint = $"{baseUrl}submissions?base64_encoded={useBase64.ToString().ToLowerInvariant()}&wait=true";

            _logger.LogInformation(
                "Submitting execution {Id} to Judge0 for language {Language} (Judge0 ID {LangId}) at {Endpoint}",
                executionId, runtimeLang, languageId, submissionEndpoint);

            string processedCode = PrepareCodeForExecution(code, languageId);
            string processedStdin = stdin ?? string.Empty;

            if (useBase64)
            {
                processedCode = Convert.ToBase64String(Encoding.UTF8.GetBytes(processedCode));
                if (!string.IsNullOrEmpty(processedStdin))
                {
                    processedStdin = Convert.ToBase64String(Encoding.UTF8.GetBytes(processedStdin));
                }
            }

            var payload = new Judge0SubmissionRequest
            {
                LanguageId = languageId,
                SourceCode = processedCode,
                Stdin = string.IsNullOrEmpty(processedStdin) ? null : processedStdin,
                CpuTimeLimit = 5.0,
                MemoryLimit = null
            };

            var jsonContent = JsonSerializer.Serialize(payload, JsonOpts);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, submissionEndpoint)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                // Add RapidAPI headers if provided, otherwise X-Auth-Token if configured
                if (!string.IsNullOrWhiteSpace(apiHost))
                {
                    request.Headers.Add("x-rapidapi-host", apiHost);
                }
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    if (!string.IsNullOrWhiteSpace(apiHost))
                    {
                        request.Headers.Add("x-rapidapi-key", apiKey);
                    }
                    else
                    {
                        request.Headers.Add("X-Auth-Token", apiKey);
                    }
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(DefaultExecutionTimeoutSeconds));

                var response = await _httpClient.SendAsync(request, cts.Token);
                stopwatch.Stop();

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Judge0 returned 429 Too Many Requests for execution {Id}", executionId);
                    return new PistonExecuteResult
                    {
                        IsRateLimited = true,
                        IsError = true,
                        ErrorMessage = "Judge0 submission quota reached (Too Many Requests).",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Judge0 execution {Id} returned HTTP {StatusCode}: {Error}",
                        executionId, response.StatusCode, errorBody);

                    return new PistonExecuteResult
                    {
                        IsError = true,
                        ExitCode = (int)response.StatusCode,
                        ErrorMessage = $"Judge0 API error (HTTP {response.StatusCode}): {errorBody}",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                var judge0Result = JsonSerializer.Deserialize<Judge0SubmissionResponse>(responseString, JsonOpts);

                if (judge0Result == null)
                {
                    return new PistonExecuteResult
                    {
                        IsError = true,
                        ErrorMessage = "Invalid or empty response received from Judge0.",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Decode base64 if enabled
                string rawStdout = DecodeIfNeeded(judge0Result.Stdout, useBase64);
                string rawStderr = DecodeIfNeeded(judge0Result.Stderr, useBase64);
                string rawCompileOutput = DecodeIfNeeded(judge0Result.CompileOutput, useBase64);
                string rawMessage = DecodeIfNeeded(judge0Result.Message, useBase64);

                var statusId = judge0Result.Status?.Id ?? 0;
                var statusDesc = judge0Result.Status?.Description ?? "Unknown";

                long execTimeMs = 0;
                if (!string.IsNullOrWhiteSpace(judge0Result.Time) &&
                    double.TryParse(judge0Result.Time, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                {
                    execTimeMs = (long)(seconds * 1000.0);
                }
                else
                {
                    execTimeMs = stopwatch.ElapsedMilliseconds;
                }

                // Judge0 Status IDs:
                // 1: In Queue, 2: Processing, 3: Accepted, 4: Wrong Answer, 5: Time Limit Exceeded,
                // 6: Compilation Error, 7-12: Runtime Errors, 13: Internal Error, 14: Exec Format Error
                bool isError = false;
                string? errorMessage = null;

                if (statusId == 5) // Time Limit Exceeded
                {
                    isError = true;
                    errorMessage = "Time Limit Exceeded (5.0s limit reached).";
                }
                else if (statusId == 6) // Compilation Error
                {
                    isError = true;
                    errorMessage = rawCompileOutput ?? "Compilation Error";
                }
                else if (statusId >= 7 && statusId <= 12) // Runtime Error
                {
                    isError = true;
                    errorMessage = !string.IsNullOrWhiteSpace(rawStderr) ? rawStderr : (rawMessage ?? statusDesc);
                }
                else if (statusId >= 13) // Internal / Exec Format Error
                {
                    isError = true;
                    errorMessage = rawMessage ?? statusDesc;
                }

                return new PistonExecuteResult
                {
                    Stdout = rawStdout ?? string.Empty,
                    Stderr = !string.IsNullOrWhiteSpace(rawStderr) ? rawStderr : (rawCompileOutput ?? string.Empty),
                    ExitCode = judge0Result.ExitCode ?? (isError ? 1 : 0),
                    CompileOutput = rawCompileOutput,
                    IsError = isError,
                    ErrorMessage = errorMessage,
                    ExecutionTimeMs = execTimeMs
                };
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to communicate with Judge0 at {Endpoint}", submissionEndpoint);
                return new PistonExecuteResult
                {
                    IsError = true,
                    ErrorMessage = $"Judge0 execution service is unavailable at {baseUrl}. Ensure the Docker container is running.",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error executing code with Judge0 for execution {Id}", executionId);
                return new PistonExecuteResult
                {
                    IsError = true,
                    ErrorMessage = $"Judge0 execution exception: {ex.Message}",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private static string DecodeIfNeeded(string? value, bool useBase64)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (!useBase64) return value;

            try
            {
                var bytes = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return value;
            }
        }

        private static string PrepareCodeForExecution(string code, int languageId)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            var trimmed = code.Trim();

            // 1. C# (ID 51): If code does not contain a class/Main, wrap bare statements into Program.Main
            if (languageId == 51)
            {
                bool hasClass = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\bclass\s+\w+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                bool hasMain = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\bstatic\s+(void|int|async\s+Task)\s+Main\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!hasClass && !hasMain)
                {
                    var lines = trimmed.Split('\n');
                    var usingDirectives = new StringBuilder();
                    var body = new StringBuilder();

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.TrimEnd('\r');
                        if (line.TrimStart().StartsWith("using ") && line.TrimEnd().EndsWith(";"))
                        {
                            usingDirectives.AppendLine(line);
                        }
                        else
                        {
                            body.AppendLine("        " + line);
                        }
                    }

                    return $@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
{usingDirectives}
public class Program
{{
    public static void Main(string[] args)
    {{
{body}
    }}
}}";
                }
                return trimmed;
            }

            // 2. Java (ID 62): If code does not contain a class, wrap bare statements into public class Main.
            // Also alias 'public class Solution' to 'public class Main' because Judge0 compiles Main.java.
            if (languageId == 62)
            {
                bool hasClass = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\bclass\s+\w+");

                if (!hasClass)
                {
                    var lines = trimmed.Split('\n');
                    var imports = new StringBuilder();
                    var body = new StringBuilder();

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.TrimEnd('\r');
                        if (line.TrimStart().StartsWith("import ") || line.TrimStart().StartsWith("package "))
                        {
                            imports.AppendLine(line);
                        }
                        else
                        {
                            body.AppendLine("        " + line);
                        }
                    }

                    return $@"import java.util.*;
import java.io.*;
import java.lang.*;
{imports}
public class Main {{
    public static void main(String[] args) {{
{body}
    }}
}}";
                }
                else
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"public\s+class\s+Solution\b"))
                    {
                        trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"public\s+class\s+Solution\b", "public class Main");
                    }
                    return trimmed;
                }
            }

            // 3. C++ (ID 54): If code does not have main(), wrap inside int main()
            if (languageId == 54)
            {
                bool hasMain = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\bmain\s*\(");

                if (!hasMain)
                {
                    var lines = trimmed.Split('\n');
                    var includes = new StringBuilder();
                    var body = new StringBuilder();

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.TrimEnd('\r');
                        var t = line.TrimStart();
                        if (t.StartsWith("#include") || t.StartsWith("using namespace"))
                        {
                            includes.AppendLine(line);
                        }
                        else
                        {
                            body.AppendLine("    " + line);
                        }
                    }

                    return $@"#include <iostream>
#include <vector>
#include <string>
#include <algorithm>
using namespace std;
{includes}
int main() {{
{body}
    return 0;
}}";
                }
                return trimmed;
            }

            return trimmed;
        }

        private sealed class Judge0SubmissionRequest
        {
            [JsonPropertyName("language_id")]
            public int LanguageId { get; set; }

            [JsonPropertyName("source_code")]
            public string SourceCode { get; set; } = string.Empty;

            [JsonPropertyName("stdin")]
            public string? Stdin { get; set; }

            [JsonPropertyName("cpu_time_limit")]
            public double? CpuTimeLimit { get; set; }

            [JsonPropertyName("memory_limit")]
            public int? MemoryLimit { get; set; }
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
            public int? Memory { get; set; }

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

