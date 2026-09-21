using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.InterviewPrep
{
    /// <summary>
    /// Inbound request payload for POST /api/interviewprep/generate
    /// </summary>
    public sealed class GenerateInterviewPrepRequestDto
    {
        public Guid? JobId { get; set; }

        public string? JobTitle { get; set; }

        public string? TargetRole { get; set; }

        [Required(ErrorMessage = "Job description is required to generate interview preparation guidelines.")]
        public string JobDescription { get; set; } = string.Empty;

        public string? ExperienceLevel { get; set; } // e.g. Entry, Mid-Level, Senior, Lead
    }

    /// <summary>
    /// Structured technical interview question with rubric and tips.
    /// </summary>
    public sealed class InterviewQuestionDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Question { get; set; } = string.Empty;

        public string Category { get; set; } = "Core Fundamentals"; // e.g. System Design, C# / .NET, React, SQL & Databases, Cloud & DevOps

        public string Difficulty { get; set; } = "Mid-Level"; // Junior, Mid-Level, Senior

        public string ExpectedAnswerGuideline { get; set; } = string.Empty;

        public string? SampleAnswer { get; set; }

        public List<string> KeyEvaluationPoints { get; set; } = new();

        public string? ProTip { get; set; }
    }

    /// <summary>
    /// STAR method structured behavioral interview question.
    /// </summary>
    public sealed class BehavioralQuestionDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Question { get; set; } = string.Empty;

        public string Competency { get; set; } = "Problem Solving & Conflict Resolution";

        public StarGuidanceDto StarGuidance { get; set; } = new();

        public string WhatToAvoid { get; set; } = string.Empty;
    }

    public sealed class StarGuidanceDto
    {
        public string Situation { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive response guide returned to the candidate portal.
    /// </summary>
    public sealed class InterviewPrepGuideDto
    {
        public Guid Id { get; set; }

        public Guid CandidateId { get; set; }

        public Guid? JobId { get; set; }

        public string JobTitle { get; set; } = string.Empty;

        public string TargetRole { get; set; } = string.Empty;

        public string JobDescription { get; set; } = string.Empty;

        public string RoleOverviewSummary { get; set; } = string.Empty;

        public List<InterviewQuestionDto> TechnicalQuestions { get; set; } = new();

        public List<BehavioralQuestionDto> BehavioralQuestions { get; set; } = new();

        public List<string> ProTips { get; set; } = new();

        public List<string> PreparationChecklist { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
