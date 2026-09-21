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
    /// Acts as a Technical Career Coach analyzing the Job Description to formulate structured
    /// 'Focus Areas' and 'Study Guidelines' (strictly NO direct interview questions).
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
                "Generating Technical Study Guidelines for Candidate {CandidateId}, AppId: {AppId}, JobId: {JobId}, Title: {JobTitle}",
                candidateId, request.ApplicationId, request.JobId, request.JobTitle);

            // ── Strict Stage Gate: ONLY 'Interview' Stage Permitted ─────────────────
            var eligibility = await CheckEligibilityAsync(
                candidateId,
                request.ApplicationId,
                request.JobId,
                cancellationToken);

            if (!eligibility.IsEligible)
            {
                _logger.LogWarning(
                    "Interview prep access denied for Candidate {CandidateId}. Current stage: {Status}",
                    candidateId, eligibility.ApplicationStatus);

                throw new InterviewPrepIneligibleException(
                    "Access denied. Interview preparation is strictly available only when your application reaches the 'Interview' stage.",
                    403,
                    eligibility.ApplicationStatus);
            }

            // Fetch job title/description from DB if ApplicationId or JobId provided
            string resolvedTitle = request.JobTitle ?? string.Empty;
            string resolvedDescription = request.JobDescription ?? string.Empty;

            if (request.ApplicationId.HasValue)
            {
                var app = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Include(a => a.Job)
                    .FirstOrDefaultAsync(a => a.Id == request.ApplicationId.Value, cancellationToken);

                if (app != null)
                {
                    request.JobId ??= app.JobId;
                    if (string.IsNullOrWhiteSpace(resolvedTitle) && app.Job != null)
                    {
                        resolvedTitle = app.Job.Title;
                    }
                    if (string.IsNullOrWhiteSpace(resolvedDescription) && app.Job != null)
                    {
                        resolvedDescription = app.Job.Description;
                    }
                }
            }

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

            // ── Generate Career Coach Study Guideline (Strictly NO Direct Questions) ──
            var keyTheoreticalAreas = BuildKeyTheoreticalAreas(resolvedTitle, resolvedDescription);
            var technicalCoreConcepts = BuildTechnicalCoreConcepts(resolvedTitle, resolvedDescription);
            var practicalImplementationFocus = BuildPracticalImplementationFocus(resolvedTitle, resolvedDescription);
            var proTips = BuildCareerCoachTips(targetRole);
            var checklist = BuildStudyChecklist(targetRole);
            var roleOverview = BuildRoleOverview(targetRole, resolvedDescription);

            var studyStore = new StudyGuidelineStore
            {
                KeyTheoreticalAreas = keyTheoreticalAreas,
                TechnicalCoreConcepts = technicalCoreConcepts,
                PracticalImplementationFocus = practicalImplementationFocus,
                ProTips = proTips,
                PreparationChecklist = checklist
            };

            var candidateExists = await _dbContext.Users.AnyAsync(u => u.Id == candidateId, cancellationToken);

            var guideEntity = new InterviewPrepGuide
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateExists ? candidateId : null,
                ApplicationId = request.ApplicationId,
                JobId = request.JobId,
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                JobDescription = resolvedDescription,
                RoleOverviewSummary = roleOverview,
                TechnicalQuestionsJson = JsonSerializer.Serialize(studyStore),
                BehavioralQuestionsJson = JsonSerializer.Serialize(technicalCoreConcepts),
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
                _logger.LogWarning(ex, "Could not persist InterviewPrepGuide to database: {Message}", ex.Message);
            }

            return new InterviewPrepGuideDto
            {
                Id = guideEntity.Id,
                CandidateId = guideEntity.CandidateId ?? candidateId,
                ApplicationId = guideEntity.ApplicationId,
                JobId = request.JobId,
                JobTitle = resolvedTitle,
                TargetRole = targetRole,
                JobDescription = resolvedDescription,
                RoleOverviewSummary = roleOverview,
                KeyTheoreticalAreas = keyTheoreticalAreas,
                TechnicalCoreConcepts = technicalCoreConcepts,
                PracticalImplementationFocus = practicalImplementationFocus,
                ProTips = proTips,
                PreparationChecklist = checklist,
                CreatedAt = guideEntity.CreatedAt
            };
        }

        public async Task<InterviewPrepGuideDto?> GetGuideByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.InterviewPrepGuides
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

            if (entity == null) return null;

            // Attempt to deserialize the unified StudyGuidelineStore
            var studyStore = DeserializeSafe<StudyGuidelineStore>(entity.TechnicalQuestionsJson);

            var theoreticalAreas = studyStore.KeyTheoreticalAreas?.Count > 0 
                ? studyStore.KeyTheoreticalAreas 
                : BuildKeyTheoreticalAreas(entity.JobTitle, entity.JobDescription);

            var coreConcepts = studyStore.TechnicalCoreConcepts?.Count > 0 
                ? studyStore.TechnicalCoreConcepts 
                : BuildTechnicalCoreConcepts(entity.JobTitle, entity.JobDescription);

            var practicalFocus = studyStore.PracticalImplementationFocus?.Count > 0 
                ? studyStore.PracticalImplementationFocus 
                : BuildPracticalImplementationFocus(entity.JobTitle, entity.JobDescription);

            var proTips = DeserializeSafe<List<string>>(entity.ProTipsJson);
            if (proTips.Count == 0) proTips = BuildCareerCoachTips(entity.TargetRole ?? entity.JobTitle);

            var checklist = DeserializeSafe<List<string>>(entity.ChecklistJson);
            if (checklist.Count == 0) checklist = BuildStudyChecklist(entity.TargetRole ?? entity.JobTitle);

            return new InterviewPrepGuideDto
            {
                Id = entity.Id,
                CandidateId = entity.CandidateId ?? Guid.Empty,
                ApplicationId = entity.ApplicationId,
                JobId = entity.JobId,
                JobTitle = entity.JobTitle,
                TargetRole = entity.TargetRole ?? entity.JobTitle,
                JobDescription = entity.JobDescription,
                RoleOverviewSummary = entity.RoleOverviewSummary ?? string.Empty,
                KeyTheoreticalAreas = theoreticalAreas,
                TechnicalCoreConcepts = coreConcepts,
                PracticalImplementationFocus = practicalFocus,
                ProTips = proTips,
                PreparationChecklist = checklist,
                CreatedAt = entity.CreatedAt
            };
        }

        public async Task<InterviewPrepGuideDto?> GetLatestGuideAsync(
            Guid candidateId,
            Guid? jobId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.InterviewPrepGuides
                .AsNoTracking()
                .Where(g => g.CandidateId == candidateId || g.CandidateId == null);

            if (jobId.HasValue)
            {
                query = query.Where(g => g.JobId == jobId.Value);
            }

            var entity = await query
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null) return null;

            return await GetGuideByIdAsync(entity.Id, cancellationToken);
        }

        public async Task<InterviewPrepEligibilityDto> CheckEligibilityAsync(
            Guid candidateId,
            Guid? applicationId = null,
            Guid? jobId = null,
            CancellationToken cancellationToken = default)
        {
            JobApplication? application = null;

            if (applicationId.HasValue)
            {
                application = await _dbContext.JobApplications
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == applicationId.Value, cancellationToken);
            }
            else if (jobId.HasValue)
            {
                application = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Where(a => a.CandidateId == candidateId && a.JobId == jobId.Value)
                    .OrderByDescending(a => a.AppliedDate)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                application = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Where(a => a.CandidateId == candidateId)
                    .OrderByDescending(a => a.AppliedDate)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // Check if there is an existing CV Evaluation result
            CvEvaluationResult? cvEval = null;
            if (application != null)
            {
                cvEval = await _dbContext.CvEvaluationResults
                    .AsNoTracking()
                    .Where(r => r.CandidateId == candidateId && r.JobId == application.JobId)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // If application is not found in database (e.g. preview mode or demo application in dev)
            if (application == null)
            {
                return new InterviewPrepEligibilityDto
                {
                    IsEligible = true,
                    ApplicationStatus = "Interview",
                    Message = "Candidate is eligible for interview preparation."
                };
            }

            var currentStatus = application.Status ?? "Pending";
            var cvApproval = cvEval?.ApprovalStatus ?? "Pending";

            // 1. If explicitly Rejected
            if (currentStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
                cvApproval.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return new InterviewPrepEligibilityDto
                {
                    IsEligible = false,
                    ApplicationStatus = "Rejected",
                    Message = "Access denied. Candidate is not eligible for interview preparation."
                };
            }

            // 2. Strict Requirement: ONLY 'Interview' stage permitted. (Assessment stage access is REMOVED)
            bool isEligible = currentStatus.Equals("Interview", StringComparison.OrdinalIgnoreCase);

            if (!isEligible)
            {
                return new InterviewPrepEligibilityDto
                {
                    IsEligible = false,
                    ApplicationStatus = currentStatus,
                    Message = "Interview Preparation is strictly unlocked only when your application reaches the 'Interview' stage."
                };
            }

            return new InterviewPrepEligibilityDto
            {
                IsEligible = true,
                ApplicationStatus = currentStatus,
                Message = "Candidate is eligible for interview preparation."
            };
        }

        private static T DeserializeSafe<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        // ── Technical Career Coach Generators (Strictly NO Direct Interview Questions) ──

        private static string BuildRoleOverview(string targetRole, string jobDescription)
        {
            return $"Technical Career Coach Guideline for the '{targetRole}' position. " +
                   $"Rather than testing with isolated interview questions, this guide identifies the fundamental " +
                   $"theoretical pillars, core architectural mechanisms, and hands-on implementation priorities required " +
                   $"by the hiring team. Review the focus areas below to guide your study sessions.";
        }

        private static List<StudyFocusAreaDto> BuildKeyTheoreticalAreas(string title, string description)
        {
            var combined = $"{title} {description}".ToLowerInvariant();
            var list = new List<StudyFocusAreaDto>();

            // ML / AI theoretical focus if relevant
            if (combined.Contains("machine learning") || combined.Contains("ai ") || combined.Contains("data scientist") || combined.Contains("deep learning"))
            {
                list.Add(new StudyFocusAreaDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Focus on Supervised Learning Models & Evaluation Metrics",
                    Section = "Key Theoretical Areas",
                    Priority = "High Priority",
                    EstimatedStudyTime = "45 mins",
                    Overview = "Analyze the mathematical and conceptual foundations of supervised classification and regression models versus unsupervised clustering.",
                    ConceptsToReview = new List<string>
                    {
                        "Bias-Variance Tradeoff: High bias (underfitting) versus high variance (overfitting) and regularization methods (L1 Lasso, L2 Ridge).",
                        "Evaluation Metrics: Precision, Recall, F1-Score, ROC-AUC, and Confusion Matrix interpretations across imbalanced datasets.",
                        "Optimization Theory: Cost functions, Gradient Descent variants (Stochastic, Mini-Batch, Adam), and convergence behavior."
                    },
                    PracticalApplication = "Be prepared to justify model selection (e.g. tree-based gradient boosting vs neural networks) on tabular business data under explainability constraints.",
                    CoachTip = "Ground your rationale in business trade-offs: model interpretability, compute training cost, and inference latency."
                });
            }

            // Distributed Systems Theory
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Review Distributed Systems Architecture & Reliability Theory",
                Section = "Key Theoretical Areas",
                Priority = "High Priority",
                EstimatedStudyTime = "40 mins",
                Overview = "Brush up on fundamental distributed systems principles required for modern microservices and multi-tier enterprise web applications.",
                ConceptsToReview = new List<string>
                {
                    "CAP Theorem & PACELC: Consistency versus Availability trade-offs under network partitions, and Eventual Consistency models.",
                    "Clean Architecture & DDD: Separation of concerns, domain entities, repository abstraction, and dependency inversion.",
                    "Resiliency Theory: Circuit Breakers, Retry with Exponential Backoff + Jitter, Bulkheads, and graceful degradation."
                },
                PracticalApplication = "Focus on designing asynchronous event-driven pipelines using message brokers to decouple write-heavy ingestion services.",
                CoachTip = "Avoid pitching monolithic perfection; interviewers look for candidates who proactively articulate single points of failure and failure containment."
            });

            // REST API Design Patterns
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Review REST API Design Patterns & Idempotent Contract Standards",
                Section = "Key Theoretical Areas",
                Priority = "Core Requirement",
                EstimatedStudyTime = "35 mins",
                Overview = "Revisit resource-oriented HTTP API guidelines, status code ergonomics, idempotency semantics, and contract versioning.",
                ConceptsToReview = new List<string>
                {
                    "HTTP Verb Semantics: Safe vs Idempotent methods (GET, HEAD vs PUT, DELETE vs POST).",
                    "Idempotency Keys: Header-based UUID deduplication strategies preventing duplicate processing during network retries.",
                    "Error Payload Standards: RFC 7807 Problem Details for HTTP APIs, centralized exception middleware, and standard error shapes."
                },
                PracticalApplication = "Walk through how you design pagination (Cursor-based vs Offset-based) for high-scale listings to maintain deterministic read consistency.",
                CoachTip = "Emphasize contract-first design with OpenAPI/Swagger and zero breaking changes across minor API revisions."
            });

            // Database Normalization & Indexing Theory
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Review Database Normalization, Indexing Theory & MVCC",
                Section = "Key Theoretical Areas",
                Priority = "High Priority",
                EstimatedStudyTime = "40 mins",
                Overview = "Strengthen theoretical understanding of relational database engines, indexing structures, and transaction isolation levels.",
                ConceptsToReview = new List<string>
                {
                    "Relational Normalization: 1NF, 2NF, 3NF versus intentional denormalization for read-heavy reporting views.",
                    "B-Tree Indexing Internals: Node fan-out, leaf node linked lists, composite index column ordering (Leftmost Prefix rule).",
                    "ACID & Transaction Isolation: Read Committed vs Repeatable Read vs Serializable, Dirty Reads, Non-repeatable Reads, Phantom Reads."
                },
                PracticalApplication = "Explain how PostgreSQL's Multi-Version Concurrency Control (MVCC) eliminates read locks while managing table bloat via VACUUM.",
                CoachTip = "Connect theoretical indexing to measurable business results—e.g., changing index order turning an unindexed 2.4s table scan into a 4ms index seek."
            });

            return list;
        }

        private static List<StudyFocusAreaDto> BuildTechnicalCoreConcepts(string title, string description)
        {
            var combined = $"{title} {description}".ToLowerInvariant();
            var list = new List<StudyFocusAreaDto>();

            // React Virtual DOM
            if (combined.Contains("react") || combined.Contains("frontend") || combined.Contains("full-stack") || combined.Contains("web") || combined.Contains("ui"))
            {
                list.Add(new StudyFocusAreaDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Understand React Virtual DOM & Concurrent Reconciliation",
                    Section = "Technical Core Concepts",
                    Priority = "High Priority",
                    EstimatedStudyTime = "45 mins",
                    Overview = "Deep dive into React 18's internal rendering pipeline, Fiber architecture, and how UI mutations are scheduled without dropping frames.",
                    ConceptsToReview = new List<string>
                    {
                        "Fiber Linked-List Unit of Work: Interruptible rendering, work-in-progress trees, and Reconciliation vs Commit phases.",
                        "Concurrent Features: Practical usage of useTransition and useDeferredValue for non-urgent state updates.",
                        "Render Optimization: Identifying re-render triggers, shallow prop comparison in React.memo, and stable function references with useCallback."
                    },
                    PracticalApplication = "Focus on diagnosing excessive re-renders using React DevTools Profiler and structuring component trees to isolate high-frequency state.",
                    CoachTip = "Emphasize user experience metrics: preventing UI input freezing during heavy client-side filtering or data table renders."
                });
            }

            // .NET Asynchronous Internals
            if (combined.Contains(".net") || combined.Contains("c#") || combined.Contains("backend") || combined.Contains("full-stack"))
            {
                list.Add(new StudyFocusAreaDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Review .NET Asynchronous Internals & Thread Pool Scheduling",
                    Section = "Technical Core Concepts",
                    Priority = "High Priority",
                    EstimatedStudyTime = "45 mins",
                    Overview = "Master how the CLR handles asynchronous state machines, task scheduling, and how to avoid thread pool exhaustion in production microservices.",
                    ConceptsToReview = new List<string>
                    {
                        "IAsyncStateMachine Generation: How the Roslyn compiler transforms async methods into state machines with continuation callbacks.",
                        "ThreadPool & SynchronizationContext: Why ASP.NET Core has no SynchronizationContext and how work resumes on worker threads.",
                        "Pitfalls & Starvation: Blocking on async code (.Result, .Wait(), .GetAwaiter().GetResult()) leading to thread pool starvation under load.",
                        "ValueTask vs Task: Utilizing ValueTask for allocation-free paths when operations complete synchronously from in-memory cache."
                    },
                    PracticalApplication = "Demonstrate knowledge of diagnosing thread pool starvation using dotnet-dump or dotnet-counters in containerized deployments.",
                    CoachTip = "Highlight that writing async code is not just about syntax, but about maximizing server throughput and hardware utilization."
                });
            }

            // Entity Framework Core Execution Pipeline
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Master Entity Framework Core Query Pipelines & N+1 Prevention",
                Section = "Technical Core Concepts",
                Priority = "Core Requirement",
                EstimatedStudyTime = "35 mins",
                Overview = "Understand how LINQ expression trees are translated into parameterized SQL and how to tune database access pipelines.",
                ConceptsToReview = new List<string>
                {
                    "Expression Tree Translation: Client vs Server evaluation boundaries and parameterized query generation.",
                    "Eager vs Split Queries: Resolving Cartesian product explosions in multi-collection joins using .AsSplitQuery().",
                    "Change Tracker Overhead: Leveraging .AsNoTracking() for read-only endpoints and projecting specific columns via .Select().",
                    "N+1 Query Detection: Recognizing lazy loading traps in loops and resolving them through explicit or eager .Include() graphs."
                },
                PracticalApplication = "Be prepared to demonstrate how enabling EF Core SQL logging during development uncovers hidden query execution overhead.",
                CoachTip = "Show mastery by explaining that ORMs are tools for productivity, but query performance must always be validated against generated SQL."
            });

            // Caching Topologies
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Review Caching Topologies & Distributed Invalidation Strategies",
                Section = "Technical Core Concepts",
                Priority = "Core Requirement",
                EstimatedStudyTime = "30 mins",
                Overview = "Study multi-tier caching architectures to balance sub-millisecond read latency against data consistency requirements.",
                ConceptsToReview = new List<string>
                {
                    "Caching Patterns: Cache-Aside (Lazy Loading), Write-Through, Write-Behind, and Refresh-Ahead tradeoffs.",
                    "Distributed Caching with Redis: Data structures (Hashes, Sorted Sets), TTL expiration policies, and Redis connection multiplexing.",
                    "Cache Stampede / Thundering Herd: Mitigating simultaneous cache misses on popular keys using probabilistic early expiration or distributed locks."
                },
                PracticalApplication = "Discuss implementing memory cache for static reference metadata alongside distributed Redis for shared candidate sessions.",
                CoachTip = "Always have an answer for 'What happens when the cache fails?' (e.g. circuit breaker fallback to DB with rate-limiting)."
            });

            return list;
        }

        private static List<StudyFocusAreaDto> BuildPracticalImplementationFocus(string title, string description)
        {
            var combined = $"{title} {description}".ToLowerInvariant();
            var list = new List<StudyFocusAreaDto>();

            // Resilient HTTP Communication
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Practical Focus: Building Resilient Microservice Communication",
                Section = "Practical Implementation Focus",
                Priority = "Practical Focus",
                EstimatedStudyTime = "40 mins",
                Overview = "Hands-on implementation guidelines for connecting distributed services reliably in cloud environments.",
                ConceptsToReview = new List<string>
                {
                    "Polly Resilience Pipelines: Configuring Circuit Breaker, Retry with Jitter, and Timeout policies in .NET.",
                    "IHttpClientFactory Best Practices: Avoiding socket exhaustion, managing DNS refresh TTLs, and typed client injection.",
                    "Correlation IDs & Distributed Tracing: Propagating X-Correlation-ID across upstream headers for end-to-end request visibility."
                },
                PracticalApplication = "Practice describing a scenario where a third-party dependency degraded, and your circuit breaker protected internal services from cascading failure.",
                CoachTip = "Focus on concrete metrics: e.g. breaking circuit after 5 consecutive failures within a 10-second window, testing recovery with half-open state."
            });

            // Front-End Architecture
            if (combined.Contains("react") || combined.Contains("frontend") || combined.Contains("full-stack"))
            {
                list.Add(new StudyFocusAreaDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Practical Focus: Front-End Architecture & Bundle Optimization",
                    Section = "Practical Implementation Focus",
                    Priority = "Practical Focus",
                    EstimatedStudyTime = "40 mins",
                    Overview = "Practical workflows for structuring scalable React component trees with minimal bundle footprints and crisp interaction speeds.",
                    ConceptsToReview = new List<string>
                    {
                        "Route-Level Code Splitting: Implementing React.lazy() and Suspense boundaries for heavy chart libraries and modal dialogs.",
                        "Optimistic UI Updates: Updating local state immediately during mutations with automatic rollback on network rejection.",
                        "Form Architecture & Validation: Managing uncontrolled vs controlled inputs, debounced search filtering, and field-level validation."
                    },
                    PracticalApplication = "Review how the candidate dashboard uses responsive layouts, CSS variables, and clean tab transitions without re-fetching unnecessary server data.",
                    CoachTip = "Emphasize accessibility and visual responsiveness: ensuring loading skeletons prevent cumulative layout shifts (CLS)."
                });
            }

            // Database Query Plan Inspection
            list.Add(new StudyFocusAreaDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Practical Focus: Query Diagnostics & EXPLAIN ANALYZE Inspection",
                Section = "Practical Implementation Focus",
                Priority = "Practical Focus",
                EstimatedStudyTime = "35 mins",
                Overview = "Hands-on techniques for identifying slow database queries and optimizing schema performance in production.",
                ConceptsToReview = new List<string>
                {
                    "Reading Query Execution Plans: Identifying Sequential Scans, Index Scans, Bitmap Heap Scans, and Nested Loop Joins.",
                    "PostgreSQL Index Optimization: Choosing B-Tree for equality/range queries, partial indexes for filtered subsets, and covering indexes with INCLUDE.",
                    "Locking & Concurrency Testing: Identifying deadlock potentials between concurrent UPDATE statements and using SELECT FOR UPDATE SKIP LOCKED for task queues."
                },
                PracticalApplication = "Demonstrate familiarity with executing EXPLAIN (ANALYZE, BUFFERS) in pgAdmin or psql to pinpoint buffer cache hit ratios.",
                CoachTip = "Interviewers love hearing how you optimized a slow real-world endpoint by replacing an unindexed multi-table JOIN with a targeted composite index."
            });

            return list;
        }

        private static List<string> BuildCareerCoachTips(string targetRole)
        {
            return new List<string>
            {
                "Always Articulate Trade-Offs: Senior engineers are evaluated on how they balance competing constraints (e.g. consistency vs latency, rapid delivery vs long-term maintainability). Avoid claiming any technology is a 'silver bullet'.",
                "Structure Complex Explanations Top-Down: Begin with a clear 30-second high-level architectural summary before diving into internal execution mechanics or code syntax.",
                "Anchor Theory in Real Production Incidents: Whenever you explain a theoretical concept (like circuit breakers or indexing), reference a production scenario or technical challenge you personally addressed.",
                "Inquire About Their Architecture: During the interview, ask insightful questions about their engineering bottlenecks, deployment cadence, and tech debt priorities to demonstrate senior engineering curiosity."
            };
        }

        private static List<string> BuildStudyChecklist(string targetRole)
        {
            return new List<string>
            {
                "Review the Key Theoretical Areas (Distributed Architecture, REST Standards, Database Normalization)",
                "Deep dive into Technical Core Concepts (Virtual DOM reconciliation, CLR async scheduling, EF Core pipelines)",
                "Walk through Practical Implementation scenarios (Resilient HTTP calls, query plan inspection with EXPLAIN ANALYZE)",
                "Prepare 2-3 concise architectural case studies from your past experience detailing technical trade-offs",
                "Conduct a 30-minute review of the employer's domain, engineering stack, and product architecture"
            };
        }
    }

    /// <summary>
    /// Storage container serialized into the database for full fidelity study guidelines.
    /// </summary>
    internal sealed class StudyGuidelineStore
    {
        public List<StudyFocusAreaDto> KeyTheoreticalAreas { get; set; } = new();
        public List<StudyFocusAreaDto> TechnicalCoreConcepts { get; set; } = new();
        public List<StudyFocusAreaDto> PracticalImplementationFocus { get; set; } = new();
        public List<string> ProTips { get; set; } = new();
        public List<string> PreparationChecklist { get; set; } = new();
    }
}
