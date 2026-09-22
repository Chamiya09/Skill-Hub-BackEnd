using Skill_Hub_BackEnd.DTOs.Assessments;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IAssessmentService
    {
        // HR CRUD & Question Templates
        Task<AssessmentResponseDto> CreateAssessmentManualAsync(
            CreateAssessmentManualDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        Task<AssessmentResponseDto> UpdateAssessmentAsync(
            Guid id,
            UpdateAssessmentDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        Task<AssessmentResponseDto> PublishAssessmentAsync(
            Guid id,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        Task<AssessmentResponseDto> ArchiveAssessmentAsync(
            Guid id,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AssessmentResponseDto>> GetAssessmentsByJobAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AssessmentTrackSummaryDto>> GetAssessmentTracksByJobAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default);

        Task<AssessmentResponseDto> GetAssessmentByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAssessmentAsync(
            Guid id,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        Task<AssessmentResponseDto> GenerateAiAssessmentDraftAsync(
            Guid jobVacancyId,
            Guid hrManagerId,
            string? focusArea = null,
            CancellationToken cancellationToken = default);

        // Dispatching to Shortlisted Candidate (Incoming Contract from Student 2)
        Task<DispatchAssessmentResponseDto> DispatchAssessmentAsync(
            DispatchAssessmentRequestDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        // Candidate Exam Flow & Portal
        Task<IReadOnlyList<CandidateAssessmentListItemDto>> GetCandidateAssessmentsAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default);

        Task<StartExamResponseDto> StartExamAsync(
            Guid submissionId,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default);

        Task<StartExamResponseDto> GetExamPaperAsync(
            Guid submissionId,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default);

        Task<bool> LogProctorEventAsync(
            Guid submissionId,
            ProctorEventRequestDto eventDto,
            CancellationToken cancellationToken = default);

        Task<bool> BlockAssessmentAsync(
            Guid submissionId,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default);

        Task<RunCodeResponseDto> RunSampleTestAsync(
            Guid submissionId,
            RunCodeRequestDto dto,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default);

        Task<bool> SaveDraftAnswersAsync(
            Guid submissionId,
            SaveDraftAnswersRequestDto dto,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default);

        Task<SubmissionDetailDto> SubmitAnswersAsync(
            Guid submissionId,
            SubmitAnswersRequestDto answersDto,
            Guid? candidateId = null,
            CancellationToken cancellationToken = default);

        Task<SubmissionDetailDto> GetSubmissionDetailAsync(
            Guid submissionId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SubmissionDetailDto>> GetSubmissionsByJobAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default);

        Task<SubmissionDetailDto> ReviewSubmissionAsync(
            Guid submissionId,
            ManualReviewSubmissionDto dto,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        // Leaderboard & Top 5 Outgoing Contract to Student 3
        Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default);

        Task<FinalizeTop5ResponseDto> FinalizeTop5Async(
            Guid jobVacancyId,
            Guid hrManagerId,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteSubmissionAsync(
            Guid submissionId,
            Guid userId,
            bool isCandidate = false,
            CancellationToken cancellationToken = default);
    }
}

