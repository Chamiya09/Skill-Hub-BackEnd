using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Skill_Hub_BackEnd.Services.Assessments
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

        private static readonly ConcurrentDictionary<string, int> LanguageCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim CacheLock = new(1, 1);
        private static volatile bool _languagesLoaded = false;

        public (string runtimeLanguage, string version) ResolveRuntime(string language)
        {
            var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "python" or "py" or "python3" => ("python", "3.7.7"),
                "javascript" or "js" or "node" => ("javascript", "12.14.0"),
                "typescript" or "ts" => ("typescript", "3.7.4"),
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => ("csharp", ".NET Core 3.1.406"),
                "java" => ("java", "OpenJDK 14.0.1"),
                "cpp" or "c++" => ("c++", "Clang 10.0.1"),
                "go" or "golang" => ("go", "1.13.5"),
                _ => ("python", "3.7.7")
            };
        }

        /// <summary>
        /// Default static fallback language mappings for Judge0 Extra CE v1.13.1.
        /// In Extra CE: Python (Python for ML 3.7.7) is ID 10, Java is ID 4, C++ is ID 2, C# is ID 21, C is ID 1.
        /// </summary>
        public static int GetJudge0LanguageId(string runtimeLang)
        {
            return (runtimeLang ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "python" or "py" or "python3" => 10,
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => 21,
                "java" => 4,
                "cpp" or "c++" => 2,
                "c" => 1,
                "nim" => 9,
                "visualbasic" or "vb" => 20,
                "fsharp" or "f#" => 24,
                // Fallbacks if CE standard languages are queried
                "javascript" or "js" or "node" => 63,
                "typescript" or "ts" => 74,
                "go" or "golang" => 60,
                _ => 10
            };
        }

        /// <summary>
        /// Static mapping for standard Judge0 CE (non-Extra).
        /// </summary>
        public static int GetStandardCeLanguageId(string runtimeLang)
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

        /// <summary>
        /// Dynamically resolves the Judge0 language ID by querying the /languages endpoint.
        /// Caches resolved IDs and falls back to static Extra CE mappings if unreachable.
        /// </summary>
        public async Task<int> ResolveLanguageIdAsync(string runtimeLang, string judge0Url, CancellationToken cancellationToken)
        {
            var normalized = (runtimeLang ?? string.Empty).Trim().ToLowerInvariant();

            if (LanguageCache.TryGetValue(normalized, out var cachedId))
            {
                return cachedId;
            }

            if (!_languagesLoaded)
            {
                await CacheLock.WaitAsync(cancellationToken);
                try
                {
                    if (!_languagesLoaded)
                    {
                        await LoadLanguagesFromJudge0Async(judge0Url, cancellationToken);
                        _languagesLoaded = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dynamically query Judge0 /languages endpoint from {Url}. Using fallback language mappings.", judge0Url);
                }
                finally
                {
                    CacheLock.Release();
                }
            }

            if (LanguageCache.TryGetValue(normalized, out var resolvedId))
            {
                return resolvedId;
            }

            var fallbackId = GetJudge0LanguageId(normalized);
            _logger.LogInformation("Language '{Lang}' resolved via static Extra CE fallback to ID {Id}", runtimeLang, fallbackId);
            return fallbackId;
        }

        private async Task LoadLanguagesFromJudge0Async(string judge0Url, CancellationToken cancellationToken)
        {
            var endpoint = $"{judge0Url}/languages";
            using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(5000);

            var resp = await _httpClient.SendAsync(req, timeoutCts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Judge0 /languages returned HTTP {Status}", resp.StatusCode);
                return;
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            var languages = JsonSerializer.Deserialize<List<Judge0LanguageItem>>(json, JsonOpts);
            if (languages == null || languages.Count == 0) return;

            var active = languages.Where(l => !l.IsArchived).ToList();

            // Match Python (e.g., "Python for ML (3.7.7)" in Extra CE [ID: 10] or "Python (3.8.1)" in standard CE [ID: 71])
            var py = active.FirstOrDefault(l => l.Name.Contains("Python", StringComparison.OrdinalIgnoreCase) && !l.Name.Contains("MPI", StringComparison.OrdinalIgnoreCase));
            if (py != null)
            {
                LanguageCache["python"] = py.Id;
                LanguageCache["py"] = py.Id;
                LanguageCache["python3"] = py.Id;
            }

            // Match Java (e.g., "Java (OpenJDK 14.0.1)" in Extra CE [ID: 4] or "Java (OpenJDK 13.0.1)" in CE [ID: 62])
            var java = active.FirstOrDefault(l => l.Name.StartsWith("Java", StringComparison.OrdinalIgnoreCase) && !l.Name.Contains("Test", StringComparison.OrdinalIgnoreCase));
            if (java != null)
            {
                LanguageCache["java"] = java.Id;
            }

            // Match C# (e.g., "C# (.NET Core SDK 3.1.406)" in Extra CE [ID: 21] or "C# (Mono 6.6.0.161)" in CE [ID: 51])
            var cs = active.FirstOrDefault(l => l.Name.StartsWith("C#", StringComparison.OrdinalIgnoreCase) && !l.Name.Contains("Test", StringComparison.OrdinalIgnoreCase));
            if (cs != null)
            {
                LanguageCache["csharp"] = cs.Id;
                LanguageCache["cs"] = cs.Id;
                LanguageCache["c#"] = cs.Id;
                LanguageCache["csharp.net"] = cs.Id;
                LanguageCache["dotnet"] = cs.Id;
            }

            // Match C++ (e.g., "C++ (Clang 10.0.1)" in Extra CE [ID: 2] or "C++ (GCC 9.2.0)" in CE [ID: 54])
            var cpp = active.FirstOrDefault(l => l.Name.StartsWith("C++", StringComparison.OrdinalIgnoreCase) && !l.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) && !l.Name.Contains("MPI", StringComparison.OrdinalIgnoreCase));
            if (cpp != null)
            {
                LanguageCache["cpp"] = cpp.Id;
                LanguageCache["c++"] = cpp.Id;
            }

            // Match C (e.g., "C (Clang 10.0.1)" in Extra CE [ID: 1] or "C (GCC 9.2.0)" in CE [ID: 50])
            var c = active.FirstOrDefault(l => l.Name.StartsWith("C (", StringComparison.OrdinalIgnoreCase) && !l.Name.Contains("MPI", StringComparison.OrdinalIgnoreCase));
            if (c != null)
            {
                LanguageCache["c"] = c.Id;
            }

            // Match JavaScript / Node.js
            var js = active.FirstOrDefault(l => l.Name.Contains("JavaScript", StringComparison.OrdinalIgnoreCase) || l.Name.Contains("Node.js", StringComparison.OrdinalIgnoreCase));
            if (js != null)
            {
                LanguageCache["javascript"] = js.Id;
                LanguageCache["js"] = js.Id;
                LanguageCache["node"] = js.Id;
            }

            // Match TypeScript
            var ts = active.FirstOrDefault(l => l.Name.Contains("TypeScript", StringComparison.OrdinalIgnoreCase));
            if (ts != null)
            {
                LanguageCache["typescript"] = ts.Id;
                LanguageCache["ts"] = ts.Id;
            }

            // Match Go
            var go = active.FirstOrDefault(l => l.Name.StartsWith("Go", StringComparison.OrdinalIgnoreCase));
            if (go != null)
            {
                LanguageCache["go"] = go.Id;
                LanguageCache["golang"] = go.Id;
            }

            _logger.LogInformation("Successfully resolved Judge0 languages dynamically ({Count} active languages found).", active.Count);
        }

        public async Task<PistonExecuteResult> ExecuteCodeAsync(
            string code,
            string language,
            string? stdin = null,
            CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString("N");
            var (runtimeLang, version) = ResolveRuntime(language);

            var judge0Url = _configuration["ExecutionEngine:Judge0Url"];
            if (string.IsNullOrWhiteSpace(judge0Url))
            {
                judge0Url = DefaultJudge0Url;
            }
            judge0Url = judge0Url.TrimEnd('/');

            var langId = await ResolveLanguageIdAsync(runtimeLang, judge0Url, cancellationToken);

            // Preprocess and smartly wrap snippet code if bare statements were submitted
            var preprocessedCode = PreprocessCodeForJudge0(code, runtimeLang);

            var submissionEndpoint = $"{judge0Url}/submissions?base64_encoded=false&wait=true";

            // Memory limit: default to 256,000 KB (256 MB) to stay well under Judge0's 512,000 KB ceiling.
            // If configured as <= 0 or null, set to null to omit the field and let Judge0 use its internal default (128 MB).
            var configuredMemLimit = _configuration.GetValue<int?>("ExecutionEngine:MemoryLimit") ?? 256000;
            int? memoryLimitPayload = configuredMemLimit > 0 ? configuredMemLimit : null;

            var payload = new Judge0SubmissionRequest
            {
                SourceCode = preprocessedCode,
                LanguageId = langId,
                Stdin = stdin ?? string.Empty,
                CpuTimeLimit = 5.0f,
                WallTimeLimit = 15.0f,
                MemoryLimit = memoryLimitPayload
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

                // If Judge0 returns asynchronously (in queue / processing or status is not yet evaluated), poll until completion
                if (submissionResult != null && !string.IsNullOrWhiteSpace(submissionResult.Token) &&
                    (submissionResult.Status == null || submissionResult.Status.Id <= 2))
                {
                    submissionResult = await PollSubmissionResultAsync(submissionResult.Token, judge0Url, stopwatch, cancellationToken)
                        ?? submissionResult;
                }

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

        private async Task<Judge0SubmissionResponse?> PollSubmissionResultAsync(
            string token,
            string judge0Url,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            var pollEndpoint = $"{judge0Url}/submissions/{token}?base64_encoded=false";

            while (stopwatch.ElapsedMilliseconds < DefaultExecutionTimeoutMs && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(400, cancellationToken);

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, pollEndpoint);
                    var resp = await _httpClient.SendAsync(req, cancellationToken);
                    if (!resp.IsSuccessStatusCode) continue;

                    var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                    var polled = JsonSerializer.Deserialize<Judge0SubmissionResponse>(json, JsonOpts);
                    if (polled?.Status != null && polled.Status.Id > 2)
                    {
                        return polled;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Transient error polling Judge0 submission {Token}: {Message}", token, ex.Message);
                }
            }

            return null;
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
                    ErrorMessage = "No response received from Judge0 execution engine (submission timed out).",
                    Stderr = "Execution timed out waiting for Judge0 sandbox response."
                };
            }

            long elapsedMs = fallbackElapsedMs;
            if (!string.IsNullOrEmpty(response.Time) &&
                float.TryParse(response.Time, NumberStyles.Any, CultureInfo.InvariantCulture, out float timeSec))
            {
                elapsedMs = (long)(timeSec * 1000);
            }

            var statusId = response.Status?.Id ?? 0;
            var statusDesc = !string.IsNullOrWhiteSpace(response.Status?.Description)
                ? response.Status.Description
                : (!string.IsNullOrWhiteSpace(response.Message)
                    ? response.Message
                    : (!string.IsNullOrWhiteSpace(response.Error) ? response.Error : "Execution completed without status description."));

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
                var compileOutput = !string.IsNullOrWhiteSpace(response.CompileOutput)
                    ? response.CompileOutput
                    : (!string.IsNullOrWhiteSpace(response.Stderr) ? response.Stderr : "Compilation failed.");

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

            // Status 7..14: Runtime Errors (NZEC, SIGSEGV, SIGXFSZ, etc.) or Incomplete status
            var errText = !string.IsNullOrWhiteSpace(response.Stderr)
                ? response.Stderr
                : (!string.IsNullOrWhiteSpace(response.CompileOutput)
                    ? response.CompileOutput
                    : (!string.IsNullOrWhiteSpace(response.Message)
                        ? response.Message
                        : (!string.IsNullOrWhiteSpace(response.Error)
                            ? response.Error
                            : (statusId > 0 ? statusDesc : "Process completed with error code but no standard error output."))));

            return new PistonExecuteResult
            {
                Stdout = response.Stdout ?? string.Empty,
                Stderr = errText,
                ExitCode = response.ExitCode ?? 1,
                IsError = true,
                ErrorMessage = response.Message ?? response.Error ?? statusDesc,
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
            public int? MemoryLimit { get; set; } = 256000;
        }

        private sealed class Judge0LanguageItem
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("is_archived")]
            public bool IsArchived { get; set; }
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

            [JsonPropertyName("error")]
            public string? Error { get; set; }

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

