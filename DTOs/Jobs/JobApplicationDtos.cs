using System;
using System.Collections.Generic;

namespace Skill_Hub_BackEnd.DTOs.Jobs
{
    public class ApplyJobDto
    {
        public string? CoverNote { get; set; }
    }

    public class JobApplicantDto
    {
        public Guid ApplicationId { get; set; }
        public Guid Id => ApplicationId;
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public Guid CandidateId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string CandidateName => FullName;
        public string Email { get; set; } = string.Empty;
        public string CandidateEmail => Email;
        public string? Phone { get; set; }
        public string? CandidatePhone => Phone;
        public string? Headline { get; set; }
        public string? CandidateHeadline => Headline;
        public string? Location { get; set; }
        public string? CandidateLocation => Location;
        public string? Experience { get; set; }
        public string? Availability { get; set; }
        public string? AvatarUrl { get; set; }
        public string? CandidateAvatarUrl => AvatarUrl;
        public string? About { get; set; }
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; } = "Applied";
        public string? CoverNote { get; set; }
        public List<string> Skills { get; set; } = new();
        public int TotalExperienceYears { get; set; }
        public string? HighestEducation { get; set; }
        public string? CurrentCompany { get; set; }
        public int? AiMatchScore { get; set; }
    }

    public class CandidateApplicationResponseDto
    {
        public Guid ApplicationId { get; set; }
        public Guid Id => ApplicationId;
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string EmploymentType { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyLogoUrl { get; set; }
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; } = "Applied";
    }

    public class ApplicationStatusDto
    {
        public bool HasApplied { get; set; }
        public DateTime? AppliedDate { get; set; }
        public string? Status { get; set; }
        public Guid? ApplicationId { get; set; }
    }
}
