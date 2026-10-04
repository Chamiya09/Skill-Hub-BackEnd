namespace Skill_Hub_BackEnd.DTOs.Admin
{
    public class AdminDashboardStatsDto
    {
        public int TotalCandidates { get; set; }
        public int ActiveJobs { get; set; }
        public int TotalAssessments { get; set; }
        public string AiApiUsage { get; set; } = string.Empty;
        public int TotalCompanies { get; set; }
        public int TotalInterviews { get; set; }
        public double SystemUptimePercent { get; set; }
        public List<AdminSystemLogDto> RecentLogs { get; set; } = new();
    }

    public class AdminSystemLogDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Action { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Status { get; set; } = "success"; // success, warning, info
    }

    public class AdminLoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
