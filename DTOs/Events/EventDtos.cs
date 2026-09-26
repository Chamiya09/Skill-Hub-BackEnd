using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Events
{
    public class CreateEventDto
    {
        [Required(ErrorMessage = "Event title is required.")]
        [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Event date is required.")]
        public DateOnly EventDate { get; set; }

        [Required(ErrorMessage = "Event time is required.")]
        [MaxLength(50, ErrorMessage = "Time cannot exceed 50 characters.")]
        public string EventTime { get; set; } = string.Empty;

        public Guid? JobVacancyId { get; set; }

        [MaxLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
        public string? Department { get; set; }
    }

    public class EventResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string EventDate { get; set; } = string.Empty; // formatted "YYYY-MM-DD" for JSON client compatibility
        public string EventTime { get; set; } = string.Empty;
        public Guid CreatedBy { get; set; }
        public string? CreatorName { get; set; }
        public Guid? JobVacancyId { get; set; }
        public string? JobVacancyTitle { get; set; }
        public string? Department { get; set; }
        public Guid? CandidateId { get; set; }
        public string? MeetingMode { get; set; }
        public string? Location { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class ScheduleCandidateInterviewDto
    {
        [Required]
        public Guid CandidateId { get; set; }

        [Required]
        public Guid JobVacancyId { get; set; }

        [Required]
        public string EventDate { get; set; } = string.Empty; // "YYYY-MM-DD"

        [Required]
        public string StartTime { get; set; } = string.Empty; // "HH:mm"

        [Required]
        public string EndTime { get; set; } = string.Empty; // "HH:mm"

        [Required]
        public string MeetingMode { get; set; } = "Online"; // "Online" or "Physical"

        [Required]
        public string Location { get; set; } = string.Empty; // Meeting URL or physical address

        public string? Notes { get; set; }

        public Guid? ExistingEventId { get; set; }
    }

    public class NationalHolidayDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Date { get; set; } = string.Empty; // "YYYY-MM-DD"
        public string Country { get; set; } = "Sri Lanka";
        public string CountryCode { get; set; } = "LK";
    }

    public class UpdateMeetingLinkDto
    {
        [Required(ErrorMessage = "Meeting link is required.")]
        public string MeetingLink { get; set; } = string.Empty;
    }
}

