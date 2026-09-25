using Skill_Hub_BackEnd.DTOs.Events;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IInterviewSchedulerService
    {
        /// <summary>
        /// Retrieves candidates in "Interview Selection" for the specified job vacancy.
        /// Consumed by the Python AI Scheduler Agent via backend HTTP.
        /// </summary>
        Task<InterviewCandidatesResponseDto> GetCandidatesForInterviewAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves existing calendar events and national holidays within a date range to identify blocked times.
        /// Consumed by the Python AI Scheduler Agent via backend HTTP.
        /// </summary>
        Task<BlockedSlotsResponseDto> GetBlockedSlotsAsync(
            Guid? companyId,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves company schedule configuration (working hours, buffers).
        /// Consumed by the Python AI Scheduler Agent via backend HTTP.
        /// </summary>
        Task<ScheduleConfigDto> GetScheduleConfigAsync(
            Guid? companyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches scheduling request to Python AI Agent microservice and returns clash-free draft schedule proposal.
        /// </summary>
        Task<ScheduleProposalResponseDto> GenerateScheduleProposalAsync(
            GenerateScheduleRequestDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Human-in-the-loop confirmation: Persists approved interview slots as Event records in the database.
        /// </summary>
        Task<ConfirmInterviewScheduleResultDto> ConfirmInterviewScheduleAsync(
            ConfirmInterviewScheduleDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves upcoming and past interviews scheduled for the authenticated candidate.
        /// </summary>
        Task<IReadOnlyList<CandidateInterviewEventDto>> GetCandidateInterviewsAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default);
    }
}
