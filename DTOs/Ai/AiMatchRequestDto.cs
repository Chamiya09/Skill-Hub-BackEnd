using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Skill_Hub_BackEnd.DTOs.Ai
{
    public sealed class AiMatchRequestDto
    {
        [Required]
        public CandidateDto Candidate { get; set; } = new();

        [Required]
        public JobDto Job { get; set; } = new();
    }

    public sealed class CandidateDto
    {
        [MinLength(1, ErrorMessage = "At least one candidate skill is required.")]
        public List<string> Skills { get; set; } = new();

        [Range(0, 80)]
        public decimal ExperienceYears { get; set; }

        public string? Headline { get; set; }
        public string? Summary { get; set; }
        public List<JsonElement> Experiences { get; set; } = new();
        public List<JsonElement> Projects { get; set; } = new();
        public List<JsonElement> Educations { get; set; } = new();
        public List<JsonElement> Certifications { get; set; } = new();
    }

    public sealed class JobDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Department { get; set; }
        public string? ExperienceLevel { get; set; }
        public string? Description { get; set; }
        public List<string> Skills { get; set; } = new();
    }
}
