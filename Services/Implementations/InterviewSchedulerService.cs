using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Events;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class InterviewSchedulerService : IInterviewSchedulerService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHolidayService _holidayService;
        private readonly ILogger<InterviewSchedulerService> _logger;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public InterviewSchedulerService(
            ApplicationDbContext dbContext,
            HttpClient httpClient,
            IConfiguration configuration,
            IHolidayService holidayService,
            ILogger<InterviewSchedulerService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _holidayService = holidayService ?? throw new ArgumentNullException(nameof(holidayService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InterviewCandidatesResponseDto> GetCandidatesForInterviewAsync(
            Guid jobVacancyId,
            CancellationToken cancellationToken = default)
        {
            var vacancy = await _dbContext.JobVacancies
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobVacancyId, cancellationToken);

            if (vacancy == null)
            {
                throw new KeyNotFoundException($"Job vacancy with ID '{jobVacancyId}' was not found.");
            }

            var candidates = new List<InterviewCandidateDto>();

            // 1. Check Submissions where IsSelectedForInterview == true
            // Prioritize candidates who are selected for interview but not yet marked "Ready for Interview"
            var interviewSubmissions = await _dbContext.Submissions
                .AsNoTracking()
                .Where(s => s.JobVacancyId == jobVacancyId && s.IsSelectedForInterview && s.Status != "Ready for Interview")
                .ToListAsync(cancellationToken);

            // If all selected candidates are already marked or none left, fall back to all selected candidates
            if (interviewSubmissions.Count == 0)
            {
                interviewSubmissions = await _dbContext.Submissions
                    .AsNoTracking()
                    .Where(s => s.JobVacancyId == jobVacancyId && s.IsSelectedForInterview)
                    .ToListAsync(cancellationToken);
            }

            if (interviewSubmissions.Count > 0)
            {
                var candidateIds = interviewSubmissions.Select(s => s.CandidateId).Distinct().ToList();
                var users = await _dbContext.Users
                    .AsNoTracking()
                    .Where(u => candidateIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

                foreach (var sub in interviewSubmissions)
                {
                    users.TryGetValue(sub.CandidateId, out var user);
                    candidates.Add(new InterviewCandidateDto
                    {
                        CandidateId = sub.CandidateId,
                        ApplicationId = sub.ApplicationId,
                        FullName = user?.FullName ?? "Candidate",
                        Email = user?.Email ?? "",
                        Score = sub.FinalWeightedScore > 0 ? sub.FinalWeightedScore : sub.ExamScore
                    });
                }
            }

            // 2. If no submissions exist yet or are flagged, check shortlisted/interview job applications
            if (candidates.Count == 0)
            {
                var applications = await _dbContext.JobApplications
                    .AsNoTracking()
                    .Include(a => a.Candidate)
                    .Where(a => a.JobId == jobVacancyId &&
                                (a.Status == "Shortlisted" || a.Status == "Interview" || a.Status == "Reviewed"))
                    .ToListAsync(cancellationToken);

                // If still empty, fall back to any active applicants for this vacancy
                if (applications.Count == 0)
                {
                    applications = await _dbContext.JobApplications
                        .AsNoTracking()
                        .Include(a => a.Candidate)
                        .Where(a => a.JobId == jobVacancyId && a.Status != "Rejected")
                        .ToListAsync(cancellationToken);
                }

                foreach (var app in applications)
                {
                    if (app.Candidate != null)
                    {
                        candidates.Add(new InterviewCandidateDto
                        {
                            CandidateId = app.CandidateId,
                            ApplicationId = app.Id,
                            FullName = app.Candidate.FullName,
                            Email = app.Candidate.Email,
                            Score = null
                        });
                    }
                }
            }

            return new InterviewCandidatesResponseDto
            {
                JobVacancyId = vacancy.Id,
                JobTitle = vacancy.Title,
                Department = vacancy.Department,
                Candidates = candidates
            };
        }

        public async Task<BlockedSlotsResponseDto> GetBlockedSlotsAsync(
            Guid? companyId,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            var blockedSlots = new List<BlockedSlotDto>();

            // 1. Fetch company calendar events within date range
            var eventsQuery = _dbContext.Events.AsNoTracking()
                .Where(e => e.EventDate >= startDate && e.EventDate <= endDate);

            if (companyId.HasValue && companyId.Value != Guid.Empty)
            {
                eventsQuery = eventsQuery.Where(e => e.CompanyId == companyId || e.CreatedBy == companyId);
            }

            var events = await eventsQuery.ToListAsync(cancellationToken);

            foreach (var ev in events)
            {
                var (start, end) = ParseEventTimes(ev.EventTime);
                blockedSlots.Add(new BlockedSlotDto
                {
                    Id = ev.Id.ToString(),
                    Title = ev.Title,
                    Date = ev.EventDate.ToString("yyyy-MM-dd"),
                    StartTime = start,
                    EndTime = end,
                    Type = "CompanyEvent"
                });
            }

            // 2. Fetch national holidays within date range
            try
            {
                var startYear = startDate.Year;
                var endYear = endDate.Year;

                for (var y = startYear; y <= endYear; y++)
                {
                    var holidays = await _holidayService.GetHolidaysAsync(y, null, "LK", cancellationToken);
                    foreach (var h in holidays)
                    {
                        if (DateOnly.TryParse(h.Date, out var hDate) && hDate >= startDate && hDate <= endDate)
                        {
                            blockedSlots.Add(new BlockedSlotDto
                            {
                                Id = h.Id,
                                Title = $"Holiday: {h.Title}",
                                Date = h.Date,
                                StartTime = "00:00",
                                EndTime = "23:59",
                                Type = "Holiday"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to include national holidays in blocked slots lookup");
            }

            return new BlockedSlotsResponseDto { BlockedSlots = blockedSlots };
        }

        public Task<ScheduleConfigDto> GetScheduleConfigAsync(
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ScheduleConfigDto
            {
                WorkingHoursStart = "09:00",
                WorkingHoursEnd = "17:00",
                BufferMinutes = 10,
                Timezone = "Asia/Colombo"
            });
        }

        public async Task<ScheduleProposalResponseDto> GenerateScheduleProposalAsync(
            GenerateScheduleRequestDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var baseUrl = _configuration["AiAgent:BaseUrl"] ?? "http://127.0.0.1:8000";
            var url = $"{baseUrl.TrimEnd('/')}/api/interview-scheduler/generate-schedule";

            if (!companyId.HasValue || companyId.Value == Guid.Empty)
            {
                var vacancy = await _dbContext.JobVacancies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.Id == dto.JobVacancyId, cancellationToken);
                if (vacancy != null && vacancy.CompanyId != Guid.Empty)
                {
                    companyId = vacancy.CompanyId;
                }
            }

            var pythonRequest = new
            {
                job_vacancy_id = dto.JobVacancyId.ToString(),
                company_id = companyId?.ToString(),
                start_date = dto.StartDate.ToString("yyyy-MM-dd"),
                end_date = dto.EndDate.ToString("yyyy-MM-dd"),
                interview_duration_minutes = dto.InterviewDurationMinutes > 0 ? dto.InterviewDurationMinutes : 30,
                parallel_tracks = dto.ParallelTracks > 0 ? dto.ParallelTracks : 2,
                working_hours_start = string.IsNullOrWhiteSpace(dto.WorkingHoursStart) ? "09:00" : dto.WorkingHoursStart,
                working_hours_end = string.IsNullOrWhiteSpace(dto.WorkingHoursEnd) ? "17:00" : dto.WorkingHoursEnd,
                buffer_minutes = dto.BufferMinutes >= 0 ? dto.BufferMinutes : 10
            };

            _logger.LogInformation("Calling Python AI Interview Scheduler at: {Url} for Job ID: {JobId}", url, dto.JobVacancyId);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(url, pythonRequest, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not reach Python AI Agent at {Url}. Is the service running?", url);
                throw new InvalidOperationException($"Unable to connect to the AI Agent microservice at '{url}'. Ensure the Python FastAPI server is running on port 8000.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("AI Agent returned error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"AI Agent error: {errorBody}");
            }

            var proposal = await response.Content.ReadFromJsonAsync<ScheduleProposalResponseDto>(JsonOpts, cancellationToken);
            return proposal ?? new ScheduleProposalResponseDto
            {
                JobVacancyId = dto.JobVacancyId,
                JobTitle = "Interview Schedule",
                Summary = new ScheduleSummaryDto { AssumptionsMade = new List<string> { "Empty proposal returned from AI agent" } }
            };
        }

        public async Task<ConfirmInterviewScheduleResultDto> ConfirmInterviewScheduleAsync(
            ConfirmInterviewScheduleDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (dto.Slots == null || dto.Slots.Count == 0)
            {
                return new ConfirmInterviewScheduleResultDto
                {
                    ScheduledCount = 0,
                    Message = "No slots were provided to confirm."
                };
            }

            var createdIds = new List<Guid>();

            JobVacancy? vacancy = null;
            if (dto.JobVacancyId != Guid.Empty)
            {
                vacancy = await _dbContext.JobVacancies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.Id == dto.JobVacancyId, cancellationToken);
            }

            foreach (var slot in dto.Slots)
            {
                if (!DateOnly.TryParse(slot.Date, out var eventDate))
                    continue;

                var cleanStart = string.IsNullOrWhiteSpace(slot.StartTime) ? "09:00" : slot.StartTime.Trim();
                var cleanEnd = string.IsNullOrWhiteSpace(slot.EndTime) ? "09:30" : slot.EndTime.Trim();
                var timeRange = $"{cleanStart} - {cleanEnd}";

                var trackName = string.IsNullOrWhiteSpace(slot.TrackName)
                    ? $"Track {slot.TrackNumber}"
                    : slot.TrackName;

                var meetingMode = string.IsNullOrWhiteSpace(slot.MeetingMode) ? "Online" : slot.MeetingMode.Trim();
                var location = string.IsNullOrWhiteSpace(slot.Location)
                    ? (meetingMode == "Online" ? "Google Meet (Link will be emailed)" : "Company Office")
                    : slot.Location.Trim();

                var newEvent = new Event
                {
                    Id = Guid.NewGuid(),
                    Title = $"Interview: {slot.CandidateName} ({trackName})",
                    Description = $"Candidate: {slot.CandidateName} ({slot.CandidateEmail})\nRole: {dto.JobTitle}\nParallel Track: {trackName}\nMode: {meetingMode}\nLocation: {location}",
                    EventDate = eventDate,
                    EventTime = timeRange,
                    CreatedBy = hrManagerId,
                    CompanyId = companyId,
                    JobVacancyId = dto.JobVacancyId != Guid.Empty ? dto.JobVacancyId : null,
                    Department = vacancy?.Department,
                    CandidateId = slot.CandidateId,
                    MeetingMode = meetingMode,
                    Location = location,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Events.Add(newEvent);
                createdIds.Add(newEvent.Id);
            }

            // Update candidate status: confirmed candidates become "Selected" (scheduled), unscheduled/cancelled remain "Ready for Interview"
            var confirmedCandidateIds = dto.Slots.Select(s => s.CandidateId).Distinct().ToList();
            if (confirmedCandidateIds.Count > 0)
            {
                var subQuery = _dbContext.Submissions
                    .Where(s => confirmedCandidateIds.Contains(s.CandidateId) && s.IsSelectedForInterview);

                if (dto.JobVacancyId != Guid.Empty)
                {
                    subQuery = subQuery.Where(s => s.JobVacancyId == dto.JobVacancyId);
                }

                var submissionsToUpdate = await subQuery.ToListAsync(cancellationToken);
                foreach (var sub in submissionsToUpdate)
                {
                    sub.Status = "Selected";
                    sub.UpdatedAt = DateTime.UtcNow;
                }

                var appQuery = _dbContext.JobApplications
                    .Where(a => confirmedCandidateIds.Contains(a.CandidateId));

                if (dto.JobVacancyId != Guid.Empty)
                {
                    appQuery = appQuery.Where(a => a.JobId == dto.JobVacancyId);
                }

                var applicationsToUpdate = await appQuery.ToListAsync(cancellationToken);
                foreach (var app in applicationsToUpdate)
                {
                    app.Status = "Interview";
                    app.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Persisted {Count} confirmed interview events for Job: {JobTitle}", createdIds.Count, dto.JobTitle);

            return new ConfirmInterviewScheduleResultDto
            {
                ScheduledCount = createdIds.Count,
                CreatedEventIds = createdIds,
                Message = $"Successfully scheduled {createdIds.Count} interviews on your calendar."
            };
        }

        public async Task<IReadOnlyList<CandidateInterviewEventDto>> GetCandidateInterviewsAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default)
        {
            var candidateUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == candidateId, cancellationToken);

            var query = _dbContext.Events
                .AsNoTracking()
                .Include(e => e.JobVacancy)
                    .ThenInclude(j => j!.Company)
                .Where(e => e.CandidateId == candidateId);

            if (candidateUser != null && !string.IsNullOrWhiteSpace(candidateUser.Email))
            {
                var emailMarker = candidateUser.Email.Trim();
                query = _dbContext.Events
                    .AsNoTracking()
                    .Include(e => e.JobVacancy)
                        .ThenInclude(j => j!.Company)
                    .Where(e => e.CandidateId == candidateId ||
                                (e.CandidateId == null && e.Description != null && e.Description.Contains(emailMarker)));
            }

            var events = await query
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.EventTime)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return events.Select(e =>
            {
                var isPast = e.EventDate < today;
                return new CandidateInterviewEventDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    JobVacancyId = e.JobVacancyId,
                    JobTitle = e.JobVacancy?.Title ?? "Technical Interview",
                    CompanyName = e.JobVacancy?.Company?.CompanyName ?? "Skill-Hub Employer",
                    Department = e.Department ?? e.JobVacancy?.Department,
                    EventDate = e.EventDate.ToString("yyyy-MM-dd"),
                    EventTime = e.EventTime,
                    MeetingMode = string.IsNullOrWhiteSpace(e.MeetingMode) ? "Online" : e.MeetingMode,
                    Location = e.Location,
                    Description = e.Description,
                    Status = isPast ? "Completed" : "Upcoming",
                    CreatedAt = e.CreatedAt
                };
            }).ToList();
        }

        private static (string StartTime, string EndTime) ParseEventTimes(string eventTime)
        {
            if (string.IsNullOrWhiteSpace(eventTime))
                return ("09:00", "10:00");

            var trimmed = eventTime.Trim();
            if (trimmed.Contains(" - "))
            {
                var parts = trimmed.Split(" - ");
                return (NormalizeTime(parts[0]), NormalizeTime(parts[1]));
            }
            if (trimmed.Contains('-'))
            {
                var parts = trimmed.Split('-');
                return (NormalizeTime(parts[0]), NormalizeTime(parts[1]));
            }

            return (NormalizeTime(trimmed), NormalizeTime(trimmed));
        }

        private static string NormalizeTime(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "09:00";
            var clean = t.Trim();
            var upper = clean.ToUpperInvariant();
            var isPm = upper.Contains("PM");
            var isAm = upper.Contains("AM");

            var timeOnly = upper.Replace("AM", "").Replace("PM", "").Trim();
            var subParts = timeOnly.Split(':');
            if (subParts.Length >= 2 && int.TryParse(subParts[0], out var h) && int.TryParse(subParts[1], out var m))
            {
                if (isPm && h < 12) h += 12;
                else if (isAm && h == 12) h = 0;
                return $"{h:D2}:{m:D2}";
            }
            if (int.TryParse(timeOnly, out var singleH))
            {
                if (isPm && singleH < 12) singleH += 12;
                else if (isAm && singleH == 12) singleH = 0;
                return $"{singleH:D2}:00";
            }
            return clean;
        }
    }
}
