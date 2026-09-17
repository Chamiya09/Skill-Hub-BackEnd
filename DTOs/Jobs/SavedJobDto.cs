namespace Skill_Hub_BackEnd.DTOs.Jobs
{
    public class SavedJobDto
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyLogoUrl { get; set; }
        public string Location { get; set; } = string.Empty;
        public string EmploymentType { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
        public string? SalaryRange { get; set; }
        public DateTime PostedAt { get; set; }
        public DateTime SavedAt { get; set; }
    }
}
