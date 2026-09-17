using Skill_Hub_BackEnd.DTOs.Jobs;

namespace Skill_Hub_BackEnd.DTOs.Dashboard
{
    public class DashboardStatsDto
    {
        public int ActiveVacanciesCount { get; set; }
        public int DraftVacanciesCount { get; set; }
        public int ClosedVacanciesCount { get; set; }
        public int TotalVacanciesCount { get; set; }
        public int TotalDepartmentsCount { get; set; }
        public int TotalCandidatesCount { get; set; }
        public int CandidatesThisWeekCount { get; set; }
        public int AiScreenedCount { get; set; }
        public int AiShortlistedCount { get; set; }
        public int ShortlistedCount { get; set; }
        public int PendingInterviewsCount { get; set; }
        public int PendingAiEvaluationsCount { get; set; }
        public IEnumerable<TopTalentMatchDto> TopTalentMatches { get; set; } = new List<TopTalentMatchDto>();
        public IEnumerable<OverviewVacancyDto> VacancyMetrics { get; set; } = new List<OverviewVacancyDto>();
        public RecentAiActivityDto? RecentAiActivity { get; set; }
        public IEnumerable<JobResponseDto> RecentVacancies { get; set; } = new List<JobResponseDto>();
    }

    public class TopTalentMatchDto
    {
        public Guid CandidateId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? Headline { get; set; }
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public int MatchPercentage { get; set; }
        public DateTime EvaluatedAt { get; set; }
    }

    public class OverviewVacancyDto
    {
        public Guid JobId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ApplicantsCount { get; set; }
        public int AiScreenedCount { get; set; }
    }

    public class RecentAiActivityDto
    {
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public int MatchPercentage { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
