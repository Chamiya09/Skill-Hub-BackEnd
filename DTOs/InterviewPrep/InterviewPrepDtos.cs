using System.ComponentModel.DataAnnotations;

namespace Skill_Hub_BackEnd.DTOs.InterviewPrep
{
    /// <summary>
    /// Inbound request payload for POST /api/interviewprep/generate
    /// </summary>
    public sealed class GenerateInterviewPrepRequestDto
    {
        public Guid? CandidateId { get; set; }

        public Guid? ApplicationId { get; set; }

        public Guid? JobId { get; set; }

        public string? JobTitle { get; set; }

        public string? TargetRole { get; set; }

        public string? JobDescription { get; set; }

        public string? ExperienceLevel { get; set; } // e.g. Entry, Mid-Level, Senior, Lead
    }

    /// <summary>
    /// Response payload returned by POST /api/interviewprep/generate
    /// </summary>
    public sealed class GenerateInterviewPrepResponseDto
    {
        public Guid GuideId { get; set; }
        public Guid Id { get; set; }
        public string Message { get; set; } = "Interview preparation guide generated successfully.";
        public InterviewPrepGuideDto? Guide { get; set; }
    }

    /// <summary>
    /// Status & eligibility response for checking if candidate can access interview prep.
    /// </summary>
    public sealed class InterviewPrepEligibilityDto
    {
        public bool IsEligible { get; set; }

        public string ApplicationStatus { get; set; } = "Pending";

        public string Message { get; set; } = string.Empty;
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
    /// Career coach study guideline focus area (strictly NO direct interview questions).
    /// Focuses on theoretical and technical areas to brush up on for the role.
    /// </summary>
    public sealed class StudyFocusAreaDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Actionable focus title (e.g. "Understand React Virtual DOM & Concurrent Reconciliation")</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>UI Section: "Key Theoretical Areas", "Technical Core Concepts", or "Practical Implementation Focus"</summary>
        public string Section { get; set; } = "Key Theoretical Areas";

        public string Priority { get; set; } = "High Priority"; // "High Priority", "Core Requirement", "Practical Focus"

        public string EstimatedStudyTime { get; set; } = "30-45 mins";

        /// <summary>Career coach summary explaining why this area is critical for the job description.</summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>Theoretical and technical topics to brush up on.</summary>
        public List<string> ConceptsToReview { get; set; } = new();

        /// <summary>Hands-on architectural and practical implementation focus.</summary>
        public string PracticalApplication { get; set; } = string.Empty;

        /// <summary>Career coach advice for this focus area.</summary>
        public string CoachTip { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive response guide returned to the candidate portal.
    /// </summary>
    public sealed class InterviewPrepGuideDto
    {
        public Guid Id { get; set; }

        public Guid GuideId => Id;

        public Guid CandidateId { get; set; }

        public Guid? ApplicationId { get; set; }

        public Guid? JobId { get; set; }

        public string JobTitle { get; set; } = string.Empty;

        public string TargetRole { get; set; } = string.Empty;

        public string JobDescription { get; set; } = string.Empty;

        public string RoleOverviewSummary { get; set; } = string.Empty;

        /// <summary>Section 1: Key Theoretical Areas to brush up on.</summary>
        public List<StudyFocusAreaDto> KeyTheoreticalAreas { get; set; } = new();

        /// <summary>Section 2: Technical Core Concepts deep dive.</summary>
        public List<StudyFocusAreaDto> TechnicalCoreConcepts { get; set; } = new();

        /// <summary>Section 3: Practical Implementation Focus for hands-on systems.</summary>
        public List<StudyFocusAreaDto> PracticalImplementationFocus { get; set; } = new();

        public List<string> ProTips { get; set; } = new();

        public List<string> PreparationChecklist { get; set; } = new();

        // Legacy compatibility helpers
        public List<InterviewQuestionDto> TechnicalQuestions { get; set; } = new();
        public List<BehavioralQuestionDto> BehavioralQuestions { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
