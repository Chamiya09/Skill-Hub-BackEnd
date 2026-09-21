using Skill_Hub_BackEnd.DTOs.InterviewPrep;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    /// <summary>
    /// Contract for the AI Interview Preparation Service (Student 1 module).
    /// Generates tailored technical questions, STAR behavioral frameworks, and strategic tips.
    /// </summary>
    public interface IInterviewPrepService
    {
        /// <summary>
        /// Generates a comprehensive interview preparation guide based on an applied job or raw job description.
        /// In this phase, realistic mock data tailored to the role is generated and persisted.
        /// </summary>
        Task<InterviewPrepGuideDto> GenerateGuideAsync(
            Guid candidateId,
            GenerateInterviewPrepRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the most recently generated interview preparation guide for a candidate.
        /// </summary>
        Task<InterviewPrepGuideDto?> GetLatestGuideAsync(
            Guid candidateId,
            Guid? jobId = null,
            CancellationToken cancellationToken = default);
    }
}
