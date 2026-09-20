namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public sealed class PistonExecuteResult
    {
        public string Stdout { get; set; } = string.Empty;
        public string Stderr { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public string? CompileOutput { get; set; }
        public bool IsRateLimited { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public long ExecutionTimeMs { get; set; }
    }

    public interface IPistonExecutionService
    {
        Task<PistonExecuteResult> ExecuteCodeAsync(
            string code,
            string language,
            string? stdin = null,
            CancellationToken cancellationToken = default);

        (string runtimeLanguage, string version) ResolveRuntime(string language);
    }
}

