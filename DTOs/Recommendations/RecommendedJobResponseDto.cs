namespace Skill_Hub_BackEnd.DTOs.Recommendations
{
    public sealed class RecommendedJobResponseDto
    {
        public Guid JobId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public DateTime PostedDate { get; init; }
        public int MatchPercentage { get; init; }
        public bool IsRecommended { get; init; }
    }
}
