namespace Skill_Hub_BackEnd.Models
{
    public class Event
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateOnly EventDate { get; set; }

        public string EventTime { get; set; } = string.Empty;

        public Guid CreatedBy { get; set; }

        public Guid? CompanyId { get; set; }

        public Guid? JobVacancyId { get; set; }

        public string? Department { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Company? Company { get; set; }
        public virtual JobVacancy? JobVacancy { get; set; }
    }
}

