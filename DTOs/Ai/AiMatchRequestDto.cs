using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Ai
{
    public class AiMatchRequestDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one candidate skill is required.")]
        public List<string> CandidateSkills { get; set; } = new();

        [Range(0, 80, ErrorMessage = "Candidate experience must be between 0 and 80 years.")]
        public int CandidateExperienceYears { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one job requirement is required.")]
        public List<string> JobRequirements { get; set; } = new();
    }
}
