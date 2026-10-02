namespace Skill_Hub_BackEnd.DTOs.Admin
{
    public class AdminCandidateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> TopSkills { get; set; } = new();
        public int AiMatchAverage { get; set; } = 92;
        public string Status { get; set; } = "Active"; // Active, Suspended
        public string Location { get; set; } = string.Empty;
    }

    public class AdminCompanyDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public int ActiveJobPosts { get; set; }
        public int TotalHires { get; set; }
        public string Status { get; set; } = "Active"; // Active, Pending
        public string Tier { get; set; } = "Enterprise"; // Enterprise, ScaleUp, Startup
        public string Location { get; set; } = string.Empty;
        public string JoinedDate { get; set; } = string.Empty;
    }

    public class AdminInquiryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string SenderType { get; set; } = "Guest"; // Candidate, Company, Guest
        public string? Organization { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = "New"; // New, Resolved
        public string Priority { get; set; } = "Normal"; // High, Normal
    }

    public class CreateInquiryDto
    {
        public string Sender { get; set; } = string.Empty;
        public string SenderType { get; set; } = "Guest";
        public string? Organization { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
