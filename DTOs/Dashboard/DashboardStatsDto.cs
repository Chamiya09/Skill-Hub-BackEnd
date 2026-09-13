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
        public IEnumerable<JobResponseDto> RecentVacancies { get; set; } = new List<JobResponseDto>();
    }
}
