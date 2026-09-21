using Skill_Hub_BackEnd.DTOs.CvEvaluation;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    /// <summary>
    /// Contract for the Agentic CV Evaluation Service.
    ///
    /// This service orchestrates a three-agent workflow that evaluates a candidate's CV
    /// against a specific job vacancy and persists the structured result to the database.
    ///
    /// ┌─────────────────────────────────────────────────────────────────────────────┐
    /// │  MULTI-AGENT PIPELINE (Agentic Workflow)                                    │
    /// │                                                                             │
    /// │  1. AgentExtractor.ExtractData()                                            │
    /// │     └─ Fetches raw candidate profile data (skills, experience, education)   │
    /// │        from the database and normalises it into a structured DTO.           │
    /// │                                                                             │
    /// │  2. AgentEvaluator.EvaluateMatch()                                          │
    /// │     └─ Calls the AI model (Groq / LangGraph) with the extracted candidate  │
    /// │        data + job description. Returns: MatchScore, Strengths,              │
    /// │        MissingSkills, and a qualitative Recommendation string.              │
    /// │                                                                             │
    /// │  3. AgentValidator.ValidateBusinessRules()                                  │
    /// │     └─ Applies domain rules (mandatory skill gate, location filter,         │
    /// │        score normalisation) and appends ValidationNotes if the score        │
    /// │        is adjusted or the candidate is flagged.                             │
    /// │                                                                             │
    /// │  Result → persisted to CvEvaluationResults table (PostgreSQL)              │
    /// └─────────────────────────────────────────────────────────────────────────────┘
    /// </summary>
    public interface IAgenticCvService
    {
        /// <summary>
        /// Runs the full three-agent CV evaluation pipeline for the given candidate/job pair
        /// and persists the structured result to the database.
        /// </summary>
        /// <param name="request">Identifies the candidate, job vacancy, and application.</param>
        /// <param name="companyId">Company ID of the authenticated recruiter (used for authorization).</param>
        /// <param name="cancellationToken">Propagates operation cancellation.</param>
        /// <returns>The structured evaluation result DTO ready for React UI binding.</returns>
        Task<CvEvaluationResultDto> AnalyzeCvAsync(
            AnalyzeCvRequestDto request,
            Guid companyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finalises the Human-in-the-Loop (HITL) approval gate.
        /// Updates the CvEvaluationResult record with the recruiter's decision ("Approved" | "Rejected"),
        /// optional reviewer notes, and a timestamp.
        /// </summary>
        /// <param name="evaluationId">ID of the CvEvaluationResult to approve/reject.</param>
        /// <param name="request">The recruiter's decision and optional notes.</param>
        /// <param name="approvingUserId">User ID of the recruiter performing the action.</param>
        /// <param name="cancellationToken">Propagates operation cancellation.</param>
        /// <returns>Confirmation payload with the updated approval status.</returns>
        Task<ApproveEvaluationResponseDto> ApproveEvaluationAsync(
            Guid evaluationId,
            ApproveEvaluationRequestDto request,
            Guid approvingUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an existing evaluation result by its ID.
        /// Returns null if no evaluation has been run yet for this record.
        /// </summary>
        Task<CvEvaluationResultDto?> GetEvaluationByIdAsync(
            Guid evaluationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the most recent evaluation result for a specific candidate/job pair.
        /// Returns null if no evaluation has been run yet.
        /// </summary>
        Task<CvEvaluationResultDto?> GetLatestEvaluationAsync(
            Guid candidateId,
            Guid jobId,
            CancellationToken cancellationToken = default);
    }
}
