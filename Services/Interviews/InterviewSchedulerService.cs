using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;

namespace Skill_Hub_BackEnd.Services.Interviews
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
            var interviewSubmissions = await _dbContext.Submissions
                .AsNoTracking()
                .Where(s => s.JobVacancyId == jobVacancyId && s.IsSelectedForInterview)
                .ToListAsync(cancellationToken);

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

            ScheduleProposalResponseDto? proposal = null;

            var baseUrl = _configuration["AiAgent:BaseUrl"] ?? "http://127.0.0.1:8000";
            var url = $"{baseUrl.TrimEnd('/')}/api/interview-scheduler/generate-schedule";

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

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, pythonRequest, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    proposal = await response.Content.ReadFromJsonAsync<ScheduleProposalResponseDto>(JsonOpts, cancellationToken);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("AI Agent returned non-success code ({StatusCode}): {Body}", response.StatusCode, errorBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reach Python AI Agent at {Url}, will use resilient scheduler fallback.", url);
            }

            // If the AI Agent successfully scheduled candidates, return the proposal
            if (proposal != null && proposal.ProposedSlots != null && proposal.ProposedSlots.Count > 0)
            {
                return proposal;
            }

            // Resilient Fallback: If AI Agent returned 0 slots (e.g. cross-region network block or 0 candidates fetched by Python),
            // check database directly for eligible interview candidates and generate clash-free slots deterministically.
            _logger.LogInformation("AI Agent returned 0 proposed slots for Job ID: {JobId}. Executing resilient deterministic scheduler fallback.", dto.JobVacancyId);
            return await GenerateDeterministicScheduleProposalAsync(dto, companyId, cancellationToken);
        }

        private async Task<ScheduleProposalResponseDto> GenerateDeterministicScheduleProposalAsync(
            GenerateScheduleRequestDto dto,
            Guid? companyId,
            CancellationToken cancellationToken)
        {
            var candResponse = await GetCandidatesForInterviewAsync(dto.JobVacancyId, cancellationToken);
            var candidates = candResponse.Candidates ?? new List<InterviewCandidateDto>();
            var jobTitle = !string.IsNullOrWhiteSpace(candResponse.JobTitle) ? candResponse.JobTitle : "Interview Schedule";

            if (candidates.Count == 0)
            {
                return new ScheduleProposalResponseDto
                {
                    JobVacancyId = dto.JobVacancyId,
                    JobTitle = jobTitle,
                    ProposedSlots = new List<ProposedSlotDto>(),
                    UnscheduledCandidates = new List<UnscheduledCandidateDto>(),
                    Summary = new ScheduleSummaryDto
                    {
                        TotalCandidates = 0,
                        ScheduledCount = 0,
                        UnscheduledCount = 0,
                        OriginalDateRange = $"{dto.StartDate:yyyy-MM-dd} to {dto.EndDate:yyyy-MM-dd}",
                        EffectiveDateRange = $"{dto.StartDate:yyyy-MM-dd} to {dto.EndDate:yyyy-MM-dd}",
                        AssumptionsMade = new List<string> { "No candidates are shortlisted, reviewed, or selected for interview for this vacancy." },
                        AiValidationNotes = new List<string> { "Zero candidates available to schedule." }
                    },
                    IsDraft = true
                };
            }

            var duration = dto.InterviewDurationMinutes > 0 ? dto.InterviewDurationMinutes : 30;
            var buffer = dto.BufferMinutes >= 0 ? dto.BufferMinutes : 10;
            var tracks = dto.ParallelTracks > 0 ? dto.ParallelTracks : 2;
            var slotInterval = duration + buffer;

            var workStartStr = string.IsNullOrWhiteSpace(dto.WorkingHoursStart) ? "09:00" : dto.WorkingHoursStart;
            var workEndStr = string.IsNullOrWhiteSpace(dto.WorkingHoursEnd) ? "17:00" : dto.WorkingHoursEnd;

            var workStartMin = TimeToMinutes(workStartStr);
            var workEndMin = TimeToMinutes(workEndStr);

            // Fetch blocked slots (company events + holidays) up to 14 days forward extension
            var maxForwardDays = 14;
            var extendedEndDate = dto.EndDate.AddDays(maxForwardDays);
            var blockedResp = await GetBlockedSlotsAsync(companyId, dto.StartDate, extendedEndDate, cancellationToken);
            var blockedList = blockedResp.BlockedSlots ?? new List<BlockedSlotDto>();

            // Group blocked slots by Date (YYYY-MM-DD)
            var blockedByDate = blockedList.GroupBy(b => b.Date).ToDictionary(g => g.Key, g => g.ToList());

            var proposedSlots = new List<ProposedSlotDto>();
            var candidateQueue = new Queue<InterviewCandidateDto>(candidates);
            var slotIndex = 1;

            var currentDate = dto.StartDate;
            var effectiveEndDate = dto.StartDate;
            var isForwardExtended = false;

            while (candidateQueue.Count > 0 && currentDate <= extendedEndDate)
            {
                // Skip weekends (Saturday and Sunday)
                if (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                var dateStr = currentDate.ToString("yyyy-MM-dd");
                blockedByDate.TryGetValue(dateStr, out var daysBlocked);
                daysBlocked ??= new List<BlockedSlotDto>();

                // Check for full day holiday or block
                var isFullDayBlocked = daysBlocked.Any(b =>
                {
                    var bStart = TimeToMinutes(b.StartTime);
                    var bEnd = TimeToMinutes(b.EndTime);
                    return bStart <= workStartMin && bEnd >= workEndMin;
                });

                if (isFullDayBlocked)
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                var isCurrentExtended = currentDate > dto.EndDate;
                if (isCurrentExtended) isForwardExtended = true;

                // Iterate time slots from workStartMin to workEndMin
                for (var slotStart = workStartMin; slotStart + duration <= workEndMin && candidateQueue.Count > 0; slotStart += slotInterval)
                {
                    var slotEnd = slotStart + duration;

                    // Check if this time slot overlaps with any blocked company event/holiday
                    var hasTimeClash = daysBlocked.Any(b =>
                    {
                        var bStart = TimeToMinutes(b.StartTime);
                        var bEnd = TimeToMinutes(b.EndTime);
                        return Math.Max(slotStart, bStart) < Math.Min(slotEnd, bEnd);
                    });

                    if (hasTimeClash)
                        continue;

                    // Assign across parallel tracks
                    for (var track = 1; track <= tracks && candidateQueue.Count > 0; track++)
                    {
                        var cand = candidateQueue.Dequeue();
                        effectiveEndDate = currentDate;

                        var trackName = GetTrackRoomName(track);
                        proposedSlots.Add(new ProposedSlotDto
                        {
                            SlotId = $"slot-{slotIndex++}",
                            CandidateId = cand.CandidateId,
                            CandidateName = cand.FullName,
                            CandidateEmail = cand.Email,
                            Date = dateStr,
                            StartTime = MinutesToTime(slotStart),
                            EndTime = MinutesToTime(slotEnd),
                            TrackNumber = track,
                            TrackName = trackName,
                            IsExtendedSearch = isCurrentExtended
                        });
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            var unscheduled = candidateQueue.Select(c => new UnscheduledCandidateDto
            {
                CandidateId = c.CandidateId,
                CandidateName = c.FullName,
                CandidateEmail = c.Email,
                Reason = "No available slots within scheduled window (including 14-day forward extension) without conflicting with existing calendar appointments."
            }).ToList();

            var forwardDays = isForwardExtended ? (effectiveEndDate.DayNumber - dto.EndDate.DayNumber) : 0;
            if (forwardDays < 0) forwardDays = 0;

            var summary = new ScheduleSummaryDto
            {
                TotalCandidates = candidates.Count,
                ScheduledCount = proposedSlots.Count,
                UnscheduledCount = unscheduled.Count,
                OriginalDateRange = $"{dto.StartDate:yyyy-MM-dd} to {dto.EndDate:yyyy-MM-dd}",
                EffectiveDateRange = $"{dto.StartDate:yyyy-MM-dd} to {effectiveEndDate:yyyy-MM-dd}",
                ForwardDaysExtended = forwardDays,
                TracksUtilized = Math.Min(tracks, proposedSlots.Count),
                AssumptionsMade = new List<string>
                {
                    $"Working hours set from {workStartStr} to {workEndStr} with a {buffer}-minute buffer.",
                    $"Utilized {tracks} concurrent parallel track(s) for interview panels.",
                    $"Standard session duration of {duration} minutes per candidate.",
                    "Weekends (Saturday & Sunday) automatically excluded."
                },
                AiValidationNotes = new List<string>
                {
                    $"Generated {proposedSlots.Count} clash-free interview session(s) across {Math.Min(tracks, Math.Max(1, proposedSlots.Count))} track(s).",
                    "Validated against company events and national holidays to prevent double-booking.",
                    isForwardExtended
                        ? $"Forward search activated: extended window by {forwardDays} day(s) to accommodate candidates."
                        : "All candidates scheduled within the target date range without forward search."
                }
            };

            return new ScheduleProposalResponseDto
            {
                JobVacancyId = dto.JobVacancyId,
                JobTitle = jobTitle,
                ProposedSlots = proposedSlots,
                UnscheduledCandidates = unscheduled,
                Summary = summary,
                IsDraft = true
            };
        }

        private static int TimeToMinutes(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return 540; // 09:00
            var norm = NormalizeTime(timeStr);
            var parts = norm.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
            {
                return h * 60 + m;
            }
            return 540;
        }

        private static string MinutesToTime(int totalMinutes)
        {
            var h = totalMinutes / 60;
            var m = totalMinutes % 60;
            return $"{h:D2}:{m:D2}";
        }

        private static string GetTrackRoomName(int trackNumber)
        {
            var roomNames = new[]
            {
                "Room A (Panel 1)",
                "Room B (Panel 2)",
                "Room C (Panel 3)",
                "Room D (Executive Panel)",
                "Room E (Technical Lab)",
                "Room F (Virtual Room)"
            };
            var idx = (trackNumber - 1) % roomNames.Length;
            return $"Track {trackNumber}: {roomNames[idx]}";
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

                var existingEvent = await _dbContext.Events
                    .FirstOrDefaultAsync(e => e.CandidateId == slot.CandidateId &&
                                              (dto.JobVacancyId == Guid.Empty || e.JobVacancyId == dto.JobVacancyId), cancellationToken);

                if (existingEvent != null)
                {
                    existingEvent.Title = $"Interview: {slot.CandidateName} ({trackName})";
                    existingEvent.Description = $"Candidate: {slot.CandidateName} ({slot.CandidateEmail})\nRole: {dto.JobTitle}\nParallel Track: {trackName}\nMode: {meetingMode}\nLocation: {location}";
                    existingEvent.EventDate = eventDate;
                    existingEvent.EventTime = timeRange;
                    existingEvent.Department = vacancy?.Department ?? existingEvent.Department;
                    existingEvent.MeetingMode = meetingMode;
                    existingEvent.Location = location;
                    existingEvent.UpdatedAt = DateTime.UtcNow;
                    createdIds.Add(existingEvent.Id);
                }
                else
                {
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
            }

            // Update candidate status: confirmed candidates become "Ready for Interview", unscheduled remain "Selected"
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
                    sub.Status = "Ready for Interview";
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
                .Include(e => e.Company)
                .Where(e => e.CandidateId == candidateId);

            if (candidateUser != null && !string.IsNullOrWhiteSpace(candidateUser.Email))
            {
                var emailMarker = candidateUser.Email.Trim().ToLower();
                var nameMarker = candidateUser.FullName?.Trim().ToLower();

                query = _dbContext.Events
                    .AsNoTracking()
                    .Include(e => e.JobVacancy)
                        .ThenInclude(j => j!.Company)
                    .Include(e => e.Company)
                    .Where(e => e.CandidateId == candidateId ||
                                (e.Description != null && e.Description.ToLower().Contains(emailMarker)) ||
                                (e.CandidateId == null && nameMarker != null && e.Title.ToLower().Contains(nameMarker)));
            }

            var events = await query
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.EventTime)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Check if candidate is hired for any vacancies
            var hiredAppJobIds = await _dbContext.JobApplications
                .Where(a => a.CandidateId == candidateId && a.Status == "Hired")
                .Select(a => a.JobId)
                .ToListAsync(cancellationToken);

            var hiredSubJobIds = await _dbContext.Submissions
                .Where(s => s.CandidateId == candidateId && s.Status == "Hired")
                .Select(s => s.JobVacancyId)
                .ToListAsync(cancellationToken);

            var hiredJobIds = new HashSet<Guid>(hiredAppJobIds.Concat(hiredSubJobIds));

            var result = events.Select(e =>
            {
                var isPast = e.EventDate < today;
                var isHired = (e.JobVacancyId.HasValue && hiredJobIds.Contains(e.JobVacancyId.Value)) ||
                              (!string.IsNullOrWhiteSpace(e.Description) && e.Description.Contains("[HIRED]"));

                return new CandidateInterviewEventDto
                {
                    Id = e.Id,
                    Title = isHired ? $"Official Offer: {e.JobVacancy?.Title ?? e.Title}" : e.Title,
                    JobVacancyId = e.JobVacancyId,
                    JobTitle = e.JobVacancy?.Title ?? "Technical Interview",
                    CompanyName = e.JobVacancy?.Company?.CompanyName ?? e.Company?.CompanyName ?? "Skill-Hub Employer",
                    Department = e.Department ?? e.JobVacancy?.Department,
                    EventDate = e.EventDate.ToString("yyyy-MM-dd"),
                    EventTime = e.EventTime,
                    MeetingMode = string.IsNullOrWhiteSpace(e.MeetingMode) ? "Online" : e.MeetingMode,
                    Location = e.Location,
                    Description = e.Description,
                    Status = isHired ? "Hired" : (isPast ? "Completed" : "Upcoming"),
                    IsHired = isHired,
                    HiredMessage = isHired ? "Congratulations! You have been selected and officially hired for this role!" : null,
                    CreatedAt = e.CreatedAt
                };
            }).ToList();

            // If candidate is hired for a job that doesn't have an Event row yet, synthesize an entry so they see it in My Interviews
            var eventJobIds = events.Where(e => e.JobVacancyId.HasValue).Select(e => e.JobVacancyId!.Value).ToHashSet();
            var missingHiredJobIds = hiredJobIds.Where(jId => !eventJobIds.Contains(jId)).ToList();
            if (missingHiredJobIds.Count > 0)
            {
                var missingJobs = await _dbContext.JobVacancies
                    .Include(j => j.Company)
                    .Where(j => missingHiredJobIds.Contains(j.Id))
                    .ToListAsync(cancellationToken);

                foreach (var mj in missingJobs)
                {
                    result.Insert(0, new CandidateInterviewEventDto
                    {
                        Id = Guid.NewGuid(),
                        Title = $"Official Offer: {mj.Title}",
                        JobVacancyId = mj.Id,
                        JobTitle = mj.Title,
                        CompanyName = mj.Company?.CompanyName ?? "Skill-Hub Employer",
                        Department = mj.Department,
                        EventDate = today.ToString("yyyy-MM-dd"),
                        EventTime = "Official Offer",
                        MeetingMode = "Offer",
                        Location = "Direct Placement",
                        Description = "[HIRED] Congratulations! You have been officially hired for this role.",
                        Status = "Hired",
                        IsHired = true,
                        HiredMessage = "Congratulations! You have been selected and officially hired for this role!",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            return result;
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
