using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Jobs
{
    public class UpdateJobDto
    {
        [Required(ErrorMessage = "Job Title is required.")]
        [MaxLength(200, ErrorMessage = "Job Title cannot exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Department is required.")]
        [MaxLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        [MaxLength(150, ErrorMessage = "Location cannot exceed 150 characters.")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Employment Type is required.")]
        [MaxLength(50, ErrorMessage = "Employment Type cannot exceed 50 characters.")]
        public string EmploymentType { get; set; } = "Full-time";

        [Required(ErrorMessage = "Experience Level is required.")]
        [MaxLength(50, ErrorMessage = "Experience Level cannot exceed 50 characters.")]
        public string ExperienceLevel { get; set; } = "Mid Level";

        [MaxLength(100, ErrorMessage = "Salary Range cannot exceed 100 characters.")]
        public string? SalaryRange { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string Status { get; set; } = "Active";

        [Required(ErrorMessage = "Job Description is required.")]
        public string Description { get; set; } = string.Empty;

        public string? WhatWeOffer { get; set; }
    }
}
