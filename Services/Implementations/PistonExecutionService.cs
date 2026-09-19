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
    public class PistonExecutionService : IPistonExecutionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PistonExecutionService> _logger;
        private const string DefaultPistonExecuteUrl = "https://emkc.org/api/v2/piston/execute";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        public PistonExecutionService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PistonExecutionService> logger)
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
                "python" or "py" or "python3" => ("python", "3.10.0"),
                "javascript" or "js" or "node" => ("javascript", "18.15.0"),
                "typescript" or "ts" => ("typescript", "5.0.3"),
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => ("csharp.net", "5.0.201"),
                "java" => ("java", "15.0.2"),
                "cpp" or "c++" => ("c++", "10.2.0"),
                "go" or "golang" => ("go", "1.16.2"),
                _ => ("python", "3.10.0")
            };
        }

        private static string GetFileNameForLanguage(string runtimeLang)
        {
            return runtimeLang switch
            {
                "java" => "Solution.java",
                "csharp" or "csharp.net" => "Solution.cs",
                "python" => "solution.py",
                "javascript" => "solution.js",
                "typescript" => "solution.ts",
                "c++" => "solution.cpp",
                "go" => "main.go",
                _ => "solution.txt"
            };
        }

        public async Task<PistonExecuteResult> ExecuteCodeAsync(
            string code,
            string language,
            string? stdin = null,
            CancellationToken cancellationToken = default)
        {
            var (runtimeLang, version) = ResolveRuntime(language);

            // Read configuration options
            var customPistonUrl = _configuration["ExecutionEngine:PistonUrl"];
            var apiKey = _configuration["ExecutionEngine:ApiKey"];
            bool preferLocal = _configuration.GetValue<bool>("ExecutionEngine:PreferLocal", true);

            bool hasCustomPiston = !string.IsNullOrWhiteSpace(customPistonUrl) || !string.IsNullOrWhiteSpace(apiKey);
            bool shouldTryLocalFirst = preferLocal || !hasCustomPiston;

            // If local execution is preferred or no custom Piston host/key is specified, execute locally directly
            if (shouldTryLocalFirst && IsLocalLanguageSupported(runtimeLang))
            {
                try
                {
                    _logger.LogInformation("Executing code locally for language: {Language}", runtimeLang);
                    return await ExecuteLocallyAsync(code, runtimeLang, stdin, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Local execution encountered an exception. Falling back to remote execution...");
                    if (!hasCustomPiston)
                    {
                        return new PistonExecuteResult
                        {
                            IsError = true,
                            ErrorMessage = $"Local execution error: {ex.Message}"
                        };
                    }
                }
            }

            // Fall back to Piston remote API
            var targetUrl = !string.IsNullOrWhiteSpace(customPistonUrl) ? customPistonUrl : DefaultPistonExecuteUrl;
            var filename = GetFileNameForLanguage(runtimeLang);

            var requestPayload = new PistonApiRequest
            {
                Language = runtimeLang,
                Version = version,
                Files = new List<PistonFileItem>
                {
                    new PistonFileItem { Name = filename, Content = code }
                },
                Stdin = stdin ?? string.Empty,
                CompileTimeout = 10000,
                RunTimeout = 5000
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload, JsonOpts);
            var stopwatch = Stopwatch.StartNew();

            int maxRetries = 1;
            int delayMs = 1000;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, targetUrl)
                    {
                        Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                    };

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

                    // If public Piston requires whitelist / token (HTTP 401 or 403), seamlessly fall back to local execution
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning("Remote Piston returned HTTP {StatusCode}. Falling back to local execution engine.", response.StatusCode);
                        if (IsLocalLanguageSupported(runtimeLang))
                        {
                            return await ExecuteLocallyAsync(code, runtimeLang, stdin, cancellationToken);
                        }

                        var errText = await response.Content.ReadAsStringAsync(cancellationToken);
                        stopwatch.Stop();
                        return new PistonExecuteResult
                        {
                            IsError = true,
                            ErrorMessage = $"Execution server responded with status {(int)response.StatusCode}. {errText}",
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        };
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (attempt < maxRetries)
                        {
                            _logger.LogWarning("Piston rate limited (429). Retrying in {Delay}ms (attempt {Attempt}/{Max})...", delayMs, attempt + 1, maxRetries);
                            await Task.Delay(delayMs, cancellationToken);
                            delayMs *= 2;
                            continue;
                        }

                        if (IsLocalLanguageSupported(runtimeLang))
                        {
                            _logger.LogWarning("Piston rate limited. Falling back to local execution engine.");
                            return await ExecuteLocallyAsync(code, runtimeLang, stdin, cancellationToken);
                        }

                        stopwatch.Stop();
                        return new PistonExecuteResult
                        {
                            IsRateLimited = true,
                            IsError = true,
                            ErrorMessage = "The execution engine is currently rate limited by the public runtime provider (HTTP 429). Please wait a few moments and try again.",
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        };
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (IsLocalLanguageSupported(runtimeLang))
                        {
                            _logger.LogWarning("Piston failed with status {Code}. Falling back to local execution engine.", response.StatusCode);
                            return await ExecuteLocallyAsync(code, runtimeLang, stdin, cancellationToken);
                        }

                        var errText = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError("Piston execution failed with HTTP {Code}: {Error}", response.StatusCode, errText);
                        stopwatch.Stop();
                        return new PistonExecuteResult
                        {
                            IsError = true,
                            ErrorMessage = $"Execution server responded with status {(int)response.StatusCode}. {errText}",
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        };
                    }

                    var respJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var pistonResp = JsonSerializer.Deserialize<PistonApiResponse>(respJson, JsonOpts);
                    stopwatch.Stop();

                    if (pistonResp == null)
                    {
                        return new PistonExecuteResult
                        {
                            IsError = true,
                            ErrorMessage = "Failed to deserialize response from code execution engine.",
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                        };
                    }

                    var compileErr = pistonResp.Compile?.Stderr ?? pistonResp.Compile?.Output;
                    var runStdout = pistonResp.Run?.Stdout ?? string.Empty;
                    var runStderr = pistonResp.Run?.Stderr ?? string.Empty;
                    var exitCode = pistonResp.Run?.Code ?? (pistonResp.Compile?.Code ?? 0);

                    return new PistonExecuteResult
                    {
                        Stdout = runStdout,
                        Stderr = runStderr,
                        ExitCode = exitCode,
                        CompileOutput = string.IsNullOrWhiteSpace(compileErr) ? null : compileErr,
                        IsError = exitCode != 0 || !string.IsNullOrWhiteSpace(compileErr),
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "Error communicating with Piston. Retrying in {Delay}ms...", delayMs);
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs *= 2;
                        continue;
                    }

                    if (IsLocalLanguageSupported(runtimeLang))
                    {
                        _logger.LogWarning(ex, "Failed to reach Piston after retries. Falling back to local execution engine.");
                        return await ExecuteLocallyAsync(code, runtimeLang, stdin, cancellationToken);
                    }

                    stopwatch.Stop();
                    _logger.LogError(ex, "Failed to execute code via Piston after retries.");
                    return new PistonExecuteResult
                    {
                        IsError = true,
                        ErrorMessage = $"Failed to execute code: {ex.Message}",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }

            if (IsLocalLanguageSupported(runtimeLang))
            {
                return await ExecuteLocallyAsync(code, runtimeLang, stdin, cancellationToken);
            }

            stopwatch.Stop();
            return new PistonExecuteResult
            {
                IsError = true,
                ErrorMessage = "Execution timed out after retries.",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }

        private static bool IsLocalLanguageSupported(string runtimeLang)
        {
            return runtimeLang switch
            {
                "python" or "javascript" or "typescript" or "csharp" or "csharp.net" or "java" or "cpp" or "c++" or "go" => true,
                _ => false
            };
        }

        private async Task<PistonExecuteResult> ExecuteLocallyAsync(
            string code,
            string runtimeLang,
            string? stdin,
            CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var tempDir = Path.Combine(Path.GetTempPath(), "skillhub_exec_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                switch (runtimeLang)
                {
                    case "python":
                    {
                        var filePath = Path.Combine(tempDir, "solution.py");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);
                        var run = await RunProcessAsync("python", "-u solution.py", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    case "javascript":
                    {
                        var filePath = Path.Combine(tempDir, "solution.js");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);
                        var run = await RunProcessAsync("node", "solution.js", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    case "typescript":
                    {
                        var filePath = Path.Combine(tempDir, "solution.ts");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);
                        var run = await RunProcessAsync("node", "--experimental-strip-types solution.ts", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    case "csharp" or "csharp.net":
                    {
                        var filePath = Path.Combine(tempDir, "Solution.cs");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);
                        string cscPath = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
                        if (!File.Exists(cscPath)) cscPath = "csc";

                        var outExe = Path.Combine(tempDir, "Solution.exe");
                        var compile = await RunProcessAsync(cscPath, $"/nologo /out:\"{outExe}\" \"{filePath}\"", tempDir, null, 10000, cancellationToken);
                        if (compile.ExitCode != 0)
                        {
                            sw.Stop();
                            return new PistonExecuteResult
                            {
                                ExitCode = compile.ExitCode,
                                CompileOutput = string.IsNullOrWhiteSpace(compile.Stderr) ? compile.Stdout : compile.Stderr,
                                Stderr = compile.Stderr,
                                IsError = true,
                                ErrorMessage = "C# compilation failed.",
                                ExecutionTimeMs = sw.ElapsedMilliseconds
                            };
                        }

                        var run = await RunProcessAsync(outExe, "", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    case "java":
                    {
                        var filePath = Path.Combine(tempDir, "Solution.java");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);

                        var compile = await RunProcessAsync("javac", $"\"{filePath}\"", tempDir, null, 10000, cancellationToken);
                        if (compile.ExitCode != 0)
                        {
                            sw.Stop();
                            return new PistonExecuteResult
                            {
                                ExitCode = compile.ExitCode,
                                CompileOutput = string.IsNullOrWhiteSpace(compile.Stderr) ? compile.Stdout : compile.Stderr,
                                Stderr = compile.Stderr,
                                IsError = true,
                                ErrorMessage = "Java compilation failed.",
                                ExecutionTimeMs = sw.ElapsedMilliseconds
                            };
                        }

                        var run = await RunProcessAsync("java", "-cp . Solution", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    case "c++" or "cpp":
                    {
                        var filePath = Path.Combine(tempDir, "Solution.cpp");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);
                        var outExe = Path.Combine(tempDir, "Solution.exe");

                        var compile = await RunProcessAsync("g++", $"-O2 \"{filePath}\" -o \"{outExe}\"", tempDir, null, 10000, cancellationToken);
                        if (compile.ExitCode != 0)
                        {
                            sw.Stop();
                            return new PistonExecuteResult
                            {
                                ExitCode = compile.ExitCode,
                                CompileOutput = string.IsNullOrWhiteSpace(compile.Stderr) ? compile.Stdout : compile.Stderr,
                                Stderr = compile.Stderr,
                                IsError = true,
                                ErrorMessage = "C++ compilation failed or g++ is not installed.",
                                ExecutionTimeMs = sw.ElapsedMilliseconds
                            };
                        }

                        var run = await RunProcessAsync(outExe, "", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    case "go":
                    {
                        var filePath = Path.Combine(tempDir, "main.go");
                        await File.WriteAllTextAsync(filePath, code, Utf8WithoutBom, cancellationToken);

                        var run = await RunProcessAsync("go", "run main.go", tempDir, stdin, 7000, cancellationToken);
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            Stdout = run.Stdout,
                            Stderr = run.Stderr,
                            ExitCode = run.ExitCode,
                            IsError = run.ExitCode != 0 || run.TimedOut,
                            ErrorMessage = run.ErrorMessage,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }

                    default:
                    {
                        sw.Stop();
                        return new PistonExecuteResult
                        {
                            IsError = true,
                            ErrorMessage = $"Unsupported local runtime language '{runtimeLang}'.",
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // Ignore temp directory deletion errors
                }
            }
        }

        private sealed class ProcessExecutionResult
        {
            public string Stdout { get; set; } = string.Empty;
            public string Stderr { get; set; } = string.Empty;
            public int ExitCode { get; set; }
            public long ElapsedMs { get; set; }
            public bool TimedOut { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private async Task<ProcessExecutionResult> RunProcessAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            string? stdin = null,
            int timeoutMs = 7000,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = psi };
                if (!process.Start())
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 1,
                        Stderr = $"Failed to start process: {fileName}",
                        ErrorMessage = $"Failed to start process: {fileName}",
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(stdin))
                {
                    await process.StandardInput.WriteAsync(stdin);
                    await process.StandardInput.FlushAsync();
                }
                process.StandardInput.Close();

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var waitTask = Task.Run(() => process.WaitForExit(timeoutMs), linkedCts.Token);
                var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs, linkedCts.Token));

                if (completed != waitTask || !waitTask.Result)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return new ProcessExecutionResult
                    {
                        ExitCode = -1,
                        Stderr = $"Execution timed out (limit: {timeoutMs / 1000}s).",
                        ErrorMessage = $"Execution timed out (limit: {timeoutMs / 1000}s).",
                        TimedOut = true,
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                sw.Stop();

                return new ProcessExecutionResult
                {
                    Stdout = stdout,
                    Stderr = stderr,
                    ExitCode = process.ExitCode,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ProcessExecutionResult
                {
                    ExitCode = 1,
                    Stderr = ex.Message,
                    ErrorMessage = $"Process execution error: {ex.Message}",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }

        private sealed class PistonApiRequest
        {
            [JsonPropertyName("language")]
            public string Language { get; set; } = string.Empty;

            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;

            [JsonPropertyName("files")]
            public List<PistonFileItem> Files { get; set; } = new();

            [JsonPropertyName("stdin")]
            public string Stdin { get; set; } = string.Empty;

            [JsonPropertyName("compile_timeout")]
            public int CompileTimeout { get; set; } = 10000;

            [JsonPropertyName("run_timeout")]
            public int RunTimeout { get; set; } = 5000;
        }

        private sealed class PistonFileItem
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private sealed class PistonApiResponse
        {
            [JsonPropertyName("language")]
            public string? Language { get; set; }

            [JsonPropertyName("version")]
            public string? Version { get; set; }

            [JsonPropertyName("run")]
            public PistonRunStage? Run { get; set; }

            [JsonPropertyName("compile")]
            public PistonRunStage? Compile { get; set; }
        }

        private sealed class PistonRunStage
        {
            [JsonPropertyName("stdout")]
            public string? Stdout { get; set; }

            [JsonPropertyName("stderr")]
            public string? Stderr { get; set; }

            [JsonPropertyName("code")]
            public int? Code { get; set; }

            [JsonPropertyName("signal")]
            public string? Signal { get; set; }

            [JsonPropertyName("output")]
            public string? Output { get; set; }
        }
    }
}
