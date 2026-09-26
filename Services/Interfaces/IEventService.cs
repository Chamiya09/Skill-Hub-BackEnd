using Skill_Hub_BackEnd.DTOs.Events;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IEventService
    {
        Task<EventResponseDto> CreateEventAsync(
            CreateEventDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(
            Guid? companyId,
            Guid hrManagerId,
            int? year = null,
            int? month = null,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            string? department = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetActiveDepartmentsAsync(
            Guid? companyId,
            CancellationToken cancellationToken = default);

        Task<EventResponseDto?> GetEventByIdAsync(
            Guid id,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        Task<EventResponseDto?> UpdateEventAsync(
            Guid id,
            CreateEventDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteEventAsync(
            Guid id,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        Task<EventResponseDto> ScheduleCandidateInterviewAsync(
            ScheduleCandidateInterviewDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default);
    }
}

