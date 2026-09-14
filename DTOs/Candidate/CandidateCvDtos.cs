using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.Candidate
{
    // ==========================================
    // 1. EXPERIENCE DTOS
    // ==========================================
    public class CreateExperienceDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Company { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Location { get; set; }

        [Required, MaxLength(50)]
        public string StartDate { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? EndDate { get; set; }

        public bool IsCurrent { get; set; } = false;

        public string? Description { get; set; }
    }

    public class ExperienceDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string? EndDate { get; set; }
        public bool IsCurrent { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ==========================================
    // 2. EDUCATION DTOS
    // ==========================================
    public class CreateEducationDto
    {
        [Required, MaxLength(200)]
        public string Degree { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Institution { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? FieldOfStudy { get; set; }

        [Required, MaxLength(50)]
        public string StartYear { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? EndYear { get; set; }

        public string? Description { get; set; }
    }

    public class EducationDto
    {
        public Guid Id { get; set; }
        public string Degree { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public string? FieldOfStudy { get; set; }
        public string StartYear { get; set; } = string.Empty;
        public string? EndYear { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ==========================================
    // 3. PROJECT DTOS
    // ==========================================
    public class CreateProjectDto
    {
        [Required, MaxLength(200)]
        public string ProjectName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Role { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Link { get; set; }
    }

    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ==========================================
    // 4. SKILL DTOS
    // ==========================================
    public class CreateSkillDto
    {
        [Required, MaxLength(100)]
        public string SkillName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Category { get; set; }
    }

    public class SkillDto
    {
        public Guid Id { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ==========================================
    // 5. CERTIFICATION DTOS
    // ==========================================
    public class CreateCertificationDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string IssuingOrganization { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? IssueDate { get; set; }

        [MaxLength(500)]
        public string? CredentialUrl { get; set; }
    }

    public class CertificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string IssuingOrganization { get; set; } = string.Empty;
        public string? IssueDate { get; set; }
        public string? CredentialUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ==========================================
    // 6. ABOUT & KEY HIGHLIGHTS DTOS
    // ==========================================
    public class CandidateHighlightDto
    {
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Value { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Subtext { get; set; } = string.Empty;
    }

    public class UpdateCandidateAboutDto
    {
        public string? Summary { get; set; }
        public List<CandidateHighlightDto>? KeyHighlights { get; set; }
    }

    public class CandidateAboutDto
    {
        public string? Summary { get; set; }
        public List<CandidateHighlightDto> KeyHighlights { get; set; } = new();
    }

    // ==========================================
    // 7. AGGREGATE CV DTO
    // ==========================================
    public class CandidateCvDto
    {
        public string? Summary { get; set; }
        public List<CandidateHighlightDto> KeyHighlights { get; set; } = new();
        public List<ExperienceDto> Experiences { get; set; } = new();
        public List<EducationDto> Educations { get; set; } = new();
        public List<ProjectDto> Projects { get; set; } = new();
        public List<SkillDto> Skills { get; set; } = new();
        public List<CertificationDto> Certifications { get; set; } = new();
    }
}

