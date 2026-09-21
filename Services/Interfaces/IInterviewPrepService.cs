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
        /// Retrieves a specific interview preparation guide by its unique GuideId.
        /// </summary>
        Task<InterviewPrepGuideDto?> GetGuideByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the most recently generated interview preparation guide for a candidate.
        /// </summary>
        Task<InterviewPrepGuideDto?> GetLatestGuideAsync(
            Guid candidateId,
            Guid? jobId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether the candidate is eligible for interview preparation.
        /// Only allowed if the candidate application is in 'Assessment' or 'Interview' (or 'Shortlisted').
        /// If 'Rejected', returns not eligible with a 403 reason.
        /// </summary>
        Task<InterviewPrepEligibilityDto> CheckEligibilityAsync(
            Guid candidateId,
            Guid? applicationId = null,
            Guid? jobId = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class InterviewPrepIneligibleException : Exception
    {
        public int StatusCode { get; }
        public string Stage { get; }
        public string Status => Stage;

        public InterviewPrepIneligibleException(string message, int statusCode, string stage)
            : base(message)
        {
            StatusCode = statusCode;
            Stage = stage;
        }
    }
}
