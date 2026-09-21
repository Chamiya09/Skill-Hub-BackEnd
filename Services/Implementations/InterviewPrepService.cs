using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.InterviewPrep;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    /// <summary>
    /// Implementation of the AI Interview Preparation Guide Service (Student 1 module).
    /// Provides intelligent, context-aware mock interview prep data tailored to the specified job role.
    /// In future iterations, this connects to the Python LangGraph AI Agent.
    /// </summary>
    public sealed class InterviewPrepService : IInterviewPrepService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InterviewPrepService> _logger;

        public InterviewPrepService(
            ApplicationDbContext dbContext,
            ILogger<InterviewPrepService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<InterviewPrepGuideDto> GenerateGuideAsync(
            Guid candidateId,
            GenerateInterviewPrepRequestDto request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Generating Interview Prep Guide for Candidate {CandidateId}, JobId: {JobId}, Title: {JobTitle}",
                candidateId, request.JobId, request.JobTitle);

            // Fetch job title/description from DB if JobId provided and fields were omitted
            string resolvedTitle = request.JobTitle ?? string.Empty;
            string resolvedDescription = request.JobDescription ?? string.Empty;

            if (request.JobId.HasValue && (string.IsNullOrWhiteSpace(resolvedTitle) || string.IsNullOrWhiteSpace(resolvedDescription)))
            {
                var job = await _dbContext.JobVacancies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.Id == request.JobId.Value, cancellationToken);

                if (job != null)
                {
                    if (string.IsNullOrWhiteSpace(resolvedTitle)) resolvedTitle = job.Title;
                    if (string.IsNullOrWhiteSpace(resolvedDescription)) resolvedDescription = job.Description;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedTitle))
            {
                resolvedTitle = !string.IsNullOrWhiteSpace(request.TargetRole) 
                    ? request.TargetRole 
                    : "Full-Stack Software Engineer";
            }

            var targetRole = !string.IsNullOrWhiteSpace(request.TargetRole) 
                ? request.TargetRole 
                : resolvedTitle;

            // Generate structured mock questions tailored to the role & tech stack
            var technicalQuestions = BuildTechnicalQuestions(resolvedTitle, resolvedDescription);
            var behavioralQuestions = BuildBehavioralQuestions(resolvedTitle);
            var proTips = BuildProTips(resolvedTitle);
            var checklist = BuildPreparationChecklist(resolvedTitle);
            var roleOverview = BuildRoleOverview(targetRole, resolvedDescription);

            var guideEntity = new InterviewPrepGuide
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateId,
                JobId = request.JobId,
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                JobDescription = resolvedDescription,
                RoleOverviewSummary = roleOverview,
                TechnicalQuestionsJson = JsonSerializer.Serialize(technicalQuestions),
                BehavioralQuestionsJson = JsonSerializer.Serialize(behavioralQuestions),
                ProTipsJson = JsonSerializer.Serialize(proTips),
                ChecklistJson = JsonSerializer.Serialize(checklist),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.InterviewPrepGuides.Add(guideEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Saved InterviewPrepGuide {GuideId} to database.", guideEntity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist InterviewPrepGuide to database (returning in-memory result for preview testing).");
            }

            return new InterviewPrepGuideDto
            {
                Id = guideEntity.Id,
                CandidateId = candidateId,
                JobId = request.JobId,
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                JobDescription = resolvedDescription,
                RoleOverviewSummary = roleOverview,
                TechnicalQuestions = technicalQuestions,
                BehavioralQuestions = behavioralQuestions,
                ProTips = proTips,
                PreparationChecklist = checklist,
                CreatedAt = guideEntity.CreatedAt
            };
        }

        public async Task<InterviewPrepGuideDto?> GetLatestGuideAsync(
            Guid candidateId,
            Guid? jobId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.InterviewPrepGuides
                .AsNoTracking()
                .Where(g => g.CandidateId == candidateId);

            if (jobId.HasValue)
            {
                query = query.Where(g => g.JobId == jobId.Value);
            }

            var entity = await query
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null) return null;

            return new InterviewPrepGuideDto
            {
                Id = entity.Id,
                CandidateId = entity.CandidateId,
                JobId = entity.JobId,
                JobTitle = entity.JobTitle,
                TargetRole = entity.TargetRole ?? entity.JobTitle,
                JobDescription = entity.JobDescription,
                RoleOverviewSummary = entity.RoleOverviewSummary ?? string.Empty,
                TechnicalQuestions = DeserializeSafe<List<InterviewQuestionDto>>(entity.TechnicalQuestionsJson),
                BehavioralQuestions = DeserializeSafe<List<BehavioralQuestionDto>>(entity.BehavioralQuestionsJson),
                ProTips = DeserializeSafe<List<string>>(entity.ProTipsJson),
                PreparationChecklist = DeserializeSafe<List<string>>(entity.ChecklistJson),
                CreatedAt = entity.CreatedAt
            };
        }

        private static T DeserializeSafe<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try
            {
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        // ── Tailored Mock Generators ─────────────────────────────────────────────

        private static string BuildRoleOverview(string targetRole, string jobDescription)
        {
            return $"This interview preparation guide is customized for the '{targetRole}' position. " +
                   $"The hiring panel will assess technical depth across core architecture, hands-on coding ability, " +
                   $"and team communication under the STAR framework. Review the questions and rubric below to prepare concise, high-impact responses.";
        }

        private static List<InterviewQuestionDto> BuildTechnicalQuestions(string title, string description)
        {
            var combinedText = $"{title} {description}".ToLowerInvariant();
            var questions = new List<InterviewQuestionDto>();

            // .NET / C# Question
            if (combinedText.Contains(".net") || combinedText.Contains("c#") || combinedText.Contains("backend") || combinedText.Contains("full-stack") || combinedText.Contains("developer"))
            {
                questions.Add(new InterviewQuestionDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Question = "How does asynchronous programming work in .NET with async/await, and what are the common pitfalls regarding SynchronizationContext and Task.Run?",
                    Category = "Backend & C# / .NET",
                    Difficulty = "Senior",
                    ExpectedAnswerGuideline = "The candidate should explain state machine generation by the Roslyn compiler, TaskCompletionSource, avoiding blocking calls (.Result, .Wait()) which lead to thread pool starvation, and using ConfigureAwait(false) in libraries.",
                    SampleAnswer = "When a method is marked 'async', the compiler generates an IAsyncStateMachine. Awaiting an uncompleted Task yields execution back to the caller while registering a continuation callback. In server applications like ASP.NET Core, there is no custom SynchronizationContext, so continuations resume on any available ThreadPool worker thread.",
                    KeyEvaluationPoints = new List<string>
                    {
                        "Understands ThreadPool vs UI SynchronizationContext",
                        "Identifies deadlock risks with .GetAwaiter().GetResult()",
                        "Mentions ValueTask for zero-allocation high-throughput scenarios"
                    },
                    ProTip = "Highlight real-world examples of preventing thread pool exhaustion in production microservices."
                });
            }

            // React / Frontend Question
            if (combinedText.Contains("react") || combinedText.Contains("frontend") || combinedText.Contains("full-stack") || combinedText.Contains("ui") || combinedText.Contains("web"))
            {
                questions.Add(new InterviewQuestionDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Question = "Explain how React 18's Concurrent Mode and Fiber architecture optimize UI rendering performance during heavy state transitions.",
                    Category = "Frontend & React",
                    Difficulty = "Mid-Level",
                    ExpectedAnswerGuideline = "Discuss Fiber nodes as units of work that can be paused, aborted, or prioritized. Mention useTransition, useDeferredValue, and how React avoids blocking the main JavaScript thread.",
                    SampleAnswer = "Prior to Fiber, React reconciliation was synchronous and recursive. Fiber introduced a linked list of virtual stack frames. With React 18 Concurrent Features, updates can be marked as non-urgent transitions, allowing high-priority user interactions (like typing in an input) to preempt large DOM updates.",
                    KeyEvaluationPoints = new List<string>
                    {
                        "Reconciliation vs Commit phase distinction",
                        "Practical usage of useTransition and startTransition",
                        "Avoiding unnecessary re-renders with memoization (useMemo/useCallback)"
                    },
                    ProTip = "Relate this to smooth user experiences in dashboard tables and search inputs."
                });
            }

            // Database / SQL / EF Core Question
            questions.Add(new InterviewQuestionDto
            {
                Id = Guid.NewGuid().ToString(),
                Question = "How do you detect, diagnose, and resolve N+1 query problems in Entity Framework Core or PostgreSQL database layers?",
                Category = "Databases & Architecture",
                Difficulty = "Mid-Level",
                ExpectedAnswerGuideline = "Explain eager loading (.Include, .ThenInclude), split queries (.AsSplitQuery()), projection via .Select(), and logging SQL via EF Core logging or pg_stat_statements.",
                SampleAnswer = "An N+1 problem occurs when a parent query executes once and child entities are lazy-loaded individually in a loop. We resolve this by eager loading related tables using .Include(), using .AsNoTracking() for read-only queries, and compiling specific projections with .Select() to pull only needed columns.",
                KeyEvaluationPoints = new List<string>
                {
                    "Differences between Eager, Lazy, and Explicit Loading",
                    "Impact of cartesian explosion in complex JOIN queries",
                    "Database indexing strategies on foreign keys"
                },
                ProTip = "Mention enabling EF Core query logging in development or using pg_stat_statements in PostgreSQL."
            });

            // System Design / Microservices / API Design
            questions.Add(new InterviewQuestionDto
            {
                Id = Guid.NewGuid().ToString(),
                Question = "Design an idempotent RESTful API for handling payments or job applications where network retries might occur.",
                Category = "System Design & APIs",
                Difficulty = "Senior",
                ExpectedAnswerGuideline = "Describe using unique Idempotency Keys (UUIDs) passed in headers, distributed locks or database unique constraints, atomic transactions, and caching previous response payloads in Redis or PostgreSQL.",
                SampleAnswer = "The client generates a unique idempotency key for mutations. The server checks a distributed store (e.g. Redis or an IdempotencyRecords DB table). If processing, it waits or returns 409 Conflict. If already completed, it returns the cached response. If new, it completes the operation within an ACID transaction.",
                KeyEvaluationPoints = new List<string>
                {
                    "Idempotency Key headers and client retry policies",
                    "Handling race conditions with database unique indexes or distributed locks",
                    "Safe vs Idempotent HTTP methods (GET/PUT vs POST)"
                },
                ProTip = "Structure your answer from Client Request -> API Gateway -> Idempotency Filter -> Transactional DB."
            });

            return questions;
        }

        private static List<BehavioralQuestionDto> BuildBehavioralQuestions(string title)
        {
            return new List<BehavioralQuestionDto>
            {
                new BehavioralQuestionDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Question = "Describe a situation where you had a strong technical disagreement with a teammate or lead regarding architecture or implementation. How did you resolve it?",
                    Competency = "Collaboration & Conflict Management",
                    StarGuidance = new StarGuidanceDto
                    {
                        Situation = "Set the stage clearly: State the project context, the technical choice in dispute (e.g., REST vs GraphQL or SQL vs NoSQL), and why both sides had valid concerns.",
                        Task = "Clarify your responsibility: Explain that your goal was not to 'win' the argument, but to find the lowest-risk, highest-performance solution for the team.",
                        Action = "Detail your constructive actions: Did you build a quick prototype, benchmark metrics, or document trade-offs in an Architecture Decision Record (ADR)?",
                        Result = "State the positive outcome: The team agreed on objective data, delivered the feature on schedule, and strengthened technical communication."
                    },
                    WhatToAvoid = "Avoid blaming team members or saying 'I was right and they were wrong'. Focus on collaborative consensus and data-driven benchmarks."
                },
                new BehavioralQuestionDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Question = "Tell me about a high-severity production outage or critical bug you discovered. How did you handle the pressure and post-incident process?",
                    Competency = "Crisis Management & Resilience",
                    StarGuidance = new StarGuidanceDto
                    {
                        Situation = "Describe an unexpected production failure (e.g., database connection spike, bad deployment, third-party API timeout) and its business impact.",
                        Task = "Your immediate objective: Mitigate user downtime, isolate the root cause, and maintain clear status communication with stakeholders.",
                        Action = "Steps taken: Rolled back the deployment or applied hotfix, reviewed telemetry logs, stabilized traffic with circuit breakers, and led a blameless post-mortem.",
                        Result = "Quantify recovery: Restored service in under 20 minutes and introduced automated regression tests and health-check alerts to ensure zero recurrence."
                    },
                    WhatToAvoid = "Avoid dwelling on panic or finger-pointing. Interviewers want to hear about calm, methodical debugging and long-term preventative safeguards."
                }
            };
        }

        private static List<string> BuildProTips(string title)
        {
            return new List<string>
            {
                "Practice the 'Talk-While-Coding' technique: Interviewers value your thought process, assumption validation, and edge-case awareness over quiet perfection.",
                "Prepare 3 insightful questions for the interviewer: E.g., 'What does the path to production look like for a new feature?' and 'How does the engineering team manage technical debt?'.",
                "Review the Company & Tech Stack: Research the company's recent engineering blogs, open-source repositories, and platform scaling challenges.",
                "Use the STAR Technique for behavioral answers: Keep answers structured (Situation: 15%, Task: 15%, Action: 50%, Result: 20%). Always quantify the outcome."
            };
        }

        private static List<string> BuildPreparationChecklist(string title)
        {
            return new List<string>
            {
                "Review core algorithms, data structures (HashMaps, Trees, Graphs), and Big-O time/space complexity.",
                "Set up and test your development environment, webcam, microphone, and IDE keyboard shortcuts.",
                "Rehearse a concise 90-second elevator pitch summarizing your technical journey, top achievements, and why this role excites you.",
                "Prepare concrete examples of system trade-offs: Latency vs Throughput, Consistency vs Availability (CAP theorem).",
                "Review your Digital CV and project highlights so you can effortlessly deep-dive into any line item."
            };
        }
    }
}
