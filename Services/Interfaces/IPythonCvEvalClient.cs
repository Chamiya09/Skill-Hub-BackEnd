namespace Skill_Hub_BackEnd.Services.Interfaces
{
    /// <summary>
    /// Typed HTTP client that calls the Python AI Agent microservice
    /// (Skill-Hub-AI-Agent / FastAPI) for CV evaluation.
    ///
    /// Endpoint consumed: POST {AiAgent:BaseUrl}/evaluate-cv
    ///
    /// The C# AgentEvaluator (inside AgenticCvService) is the only caller.
    /// The interface exists so the implementation can be swapped in tests.
    /// </summary>
    public interface IPythonCvEvalClient
    {
        /// <summary>
        /// Sends the candidate CV text and job description to the Python
        /// microservice and returns the structured 3-agent evaluation result.
        /// </summary>
        /// <param name="cvText">
        ///   Assembled CV text from AgentExtractor (name, skills, experience, etc.)
        /// </param>
        /// <param name="jobDescription">
        ///   Full job description from the JobVacancy record.
        /// </param>
        /// <param name="cancellationToken">Propagated from the controller request.</param>
        Task<PythonCvEvalResponse> EvaluateAsync(
            string cvText,
            string jobDescription,
            CancellationToken cancellationToken = default);
    }

    // ─── Response DTO ───────────────────────────────────────────────────────────
    // Must match CvEvalResponse in the Python main.py exactly.

    public sealed class PythonCvEvalResponse
    {
        public int MatchScore              { get; init; }
        public List<string> Strengths      { get; init; } = [];
        public List<string> MissingSkills  { get; init; } = [];
        public string Recommendation       { get; init; } = string.Empty;
        public List<string> ValidationNotes { get; init; } = [];
    }
}
