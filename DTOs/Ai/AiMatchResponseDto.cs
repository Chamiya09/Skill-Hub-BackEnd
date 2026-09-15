namespace Skill_Hub_BackEnd.DTOs.Ai
{
    public class AiMatchResponseDto
    {
        public int MatchPercentage { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingSkills { get; set; } = new();
        public string AiRecommendation { get; set; } = string.Empty;
    }
}
