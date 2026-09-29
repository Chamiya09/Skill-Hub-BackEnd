using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Events
{
    public class InterviewCandidateDto
    {
        public Guid CandidateId { get; set; }
        public Guid ApplicationId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal? Score { get; set; }
    }

    public class InterviewCandidatesResponseDto
    {
        public Guid JobVacancyId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public List<InterviewCandidateDto> Candidates { get; set; } = new();
        public int TotalCount => Candidates.Count;
    }

    public class BlockedSlotDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public string StartTime { get; set; } = string.Empty; // HH:mm
        public string EndTime { get; set; } = string.Empty; // HH:mm
        public string Type { get; set; } = "Event"; // "CompanyEvent" or "Holiday"
    }

    public class BlockedSlotsResponseDto
    {
        public List<BlockedSlotDto> BlockedSlots { get; set; } = new();
    }

    public class ScheduleConfigDto
    {
        public string WorkingHoursStart { get; set; } = "09:00";
        public string WorkingHoursEnd { get; set; } = "17:00";
        public int BufferMinutes { get; set; } = 10;
        public string Timezone { get; set; } = "Asia/Colombo";
    }

    public class GenerateScheduleRequestDto
    {
        [Required]
        public Guid JobVacancyId { get; set; }

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        public int InterviewDurationMinutes { get; set; } = 30;

        public int ParallelTracks { get; set; } = 2;

        public string? WorkingHoursStart { get; set; } = "09:00";

        public string? WorkingHoursEnd { get; set; } = "17:00";

        public int BufferMinutes { get; set; } = 10;
    }

    public class ProposedSlotDto
    {
        public string SlotId { get; set; } = string.Empty;
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public string StartTime { get; set; } = string.Empty; // HH:mm
        public string EndTime { get; set; } = string.Empty; // HH:mm
        public int TrackNumber { get; set; } = 1;
        public string TrackName { get; set; } = string.Empty;
        public bool IsExtendedSearch { get; set; } = false;
    }

    public class UnscheduledCandidateDto
    {
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class ScheduleSummaryDto
    {
        public int TotalCandidates { get; set; }
        public int ScheduledCount { get; set; }
        public int UnscheduledCount { get; set; }
        public string OriginalDateRange { get; set; } = string.Empty;
        public string EffectiveDateRange { get; set; } = string.Empty;
        public int ForwardDaysExtended { get; set; }
        public int TracksUtilized { get; set; }
        public List<string> AssumptionsMade { get; set; } = new();
        public List<string> AiValidationNotes { get; set; } = new();
    }

    public class ScheduleProposalResponseDto
    {
        public Guid JobVacancyId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public List<ProposedSlotDto> ProposedSlots { get; set; } = new();
        public List<UnscheduledCandidateDto> UnscheduledCandidates { get; set; } = new();
        public ScheduleSummaryDto Summary { get; set; } = new();
        public bool IsDraft { get; set; } = true;
    }

    public class ConfirmedSlotItemDto
    {
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public string StartTime { get; set; } = string.Empty; // HH:mm
        public string EndTime { get; set; } = string.Empty; // HH:mm
        public int TrackNumber { get; set; } = 1;
        public string TrackName { get; set; } = string.Empty;
        public string MeetingMode { get; set; } = "Online"; // "Online" | "Physical"
        public string Location { get; set; } = string.Empty; // Meeting URL or Physical venue
    }

    public class ConfirmInterviewScheduleDto
    {
        [Required]
        public Guid JobVacancyId { get; set; }

        public string JobTitle { get; set; } = string.Empty;

        [Required]
        public List<ConfirmedSlotItemDto> Slots { get; set; } = new();
    }

    public class ConfirmInterviewScheduleResultDto
    {
        public int ScheduledCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<Guid> CreatedEventIds { get; set; } = new();
    }

    public class CandidateInterviewEventDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid? JobVacancyId { get; set; }
        public string? JobTitle { get; set; }
        public string? CompanyName { get; set; }
        public string? Department { get; set; }
        public string EventDate { get; set; } = string.Empty; // YYYY-MM-DD
        public string EventTime { get; set; } = string.Empty; // HH:mm - HH:mm
        public string MeetingMode { get; set; } = "Online"; // "Online" or "Physical"
        public string? Location { get; set; } // Meeting link or venue
        public string? Description { get; set; }
        public string Status { get; set; } = "Upcoming";
        public bool IsHired { get; set; } = false;
        public string? HiredMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
