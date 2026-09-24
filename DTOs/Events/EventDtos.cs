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
        public DateTime CreatedAt { get; set; }
    }
}

