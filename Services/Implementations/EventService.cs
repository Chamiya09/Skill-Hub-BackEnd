using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Events;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class EventService : IEventService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<EventService> _logger;

        public EventService(ApplicationDbContext dbContext, ILogger<EventService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EventResponseDto> CreateEventAsync(
            CreateEventDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            string? dept = string.IsNullOrWhiteSpace(dto.Department) ? null : dto.Department.Trim();
            Guid? vacancyId = dto.JobVacancyId.HasValue && dto.JobVacancyId.Value != Guid.Empty ? dto.JobVacancyId : null;

            if (vacancyId.HasValue && string.IsNullOrWhiteSpace(dept))
            {
                var vacancy = await _dbContext.JobVacancies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.Id == vacancyId.Value, cancellationToken);
                if (vacancy != null && !string.IsNullOrWhiteSpace(vacancy.Department))
                {
                    dept = vacancy.Department.Trim();
                }
            }

            var newEvent = new Event
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                EventDate = dto.EventDate,
                EventTime = dto.EventTime.Trim(),
                CreatedBy = hrManagerId,
                CompanyId = companyId,
                JobVacancyId = vacancyId,
                Department = dept,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Events.Add(newEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[EventService] HR user {HrId} created event {EventId} ('{Title}', Dept: '{Dept}') on {Date} at {Time}",
                hrManagerId, newEvent.Id, newEvent.Title, newEvent.Department, newEvent.EventDate, newEvent.EventTime);

            // Fetch creator's display name if available (from Users or Companies)
            string? creatorName = null;
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == hrManagerId, cancellationToken);
            if (user != null)
            {
                creatorName = $"{user.FirstName} {user.LastName}".Trim();
            }
            else
            {
                var comp = await _dbContext.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == hrManagerId, cancellationToken);
                if (comp != null)
                {
                    creatorName = comp.CompanyName;
                }
            }

            return MapToDto(newEvent, creatorName);
        }

        public async Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(
            Guid? companyId,
            Guid hrManagerId,
            int? year = null,
            int? month = null,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            string? department = null,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.Events
                .AsNoTracking()
                .Include(e => e.JobVacancy)
                .AsQueryable();

            // Scoping: by Company if present, or by HR Manager
            if (companyId.HasValue && companyId.Value != Guid.Empty)
            {
                query = query.Where(e => e.CompanyId == companyId.Value || e.CreatedBy == hrManagerId);
            }
            else
            {
                query = query.Where(e => e.CreatedBy == hrManagerId);
            }

            // Department filtering: checks direct event department OR linked JobVacancy department across all vacancies
            if (!string.IsNullOrWhiteSpace(department))
            {
                var cleanDept = department.Trim();
                query = query.Where(e =>
                    (e.Department != null && EF.Functions.ILike(e.Department, cleanDept)) ||
                    (e.JobVacancy != null && EF.Functions.ILike(e.JobVacancy.Department, cleanDept)));
            }

            // Date filtering
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(e => e.EventDate >= startDate.Value && e.EventDate <= endDate.Value);
            }
            else if (year.HasValue && month.HasValue)
            {
                var startOfMonth = new DateOnly(year.Value, month.Value, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
                query = query.Where(e => e.EventDate >= startOfMonth && e.EventDate <= endOfMonth);
            }
            else if (year.HasValue)
            {
                var startOfYear = new DateOnly(year.Value, 1, 1);
                var endOfYear = new DateOnly(year.Value, 12, 31);
                query = query.Where(e => e.EventDate >= startOfYear && e.EventDate <= endOfYear);
            }

            var events = await query
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.EventTime)
                .ToListAsync(cancellationToken);

            if (events.Count == 0) return Array.Empty<EventResponseDto>();

            var creatorIds = events.Select(e => e.CreatedBy).Distinct().ToList();
            var userNames = await _dbContext.Users
                .AsNoTracking()
                .Where(u => creatorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);

            var companyNames = await _dbContext.Companies
                .AsNoTracking()
                .Where(c => creatorIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.CompanyName, cancellationToken);

            return events.Select(e =>
            {
                string? name = null;
                if (userNames.TryGetValue(e.CreatedBy, out var uName)) name = uName;
                else if (companyNames.TryGetValue(e.CreatedBy, out var cName)) name = cName;
                return MapToDto(e, name);
            }).ToList();
        }

        public async Task<IReadOnlyList<string>> GetActiveDepartmentsAsync(
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.JobVacancies
                .AsNoTracking()
                .Where(j => j.Status.ToLower() == "active" && !string.IsNullOrWhiteSpace(j.Department));

            if (companyId.HasValue && companyId.Value != Guid.Empty)
            {
                var companyHasActive = await query.AnyAsync(j => j.CompanyId == companyId.Value, cancellationToken);
                if (companyHasActive)
                {
                    query = query.Where(j => j.CompanyId == companyId.Value);
                }
            }

            var departments = await query
                .Select(j => j.Department.Trim())
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync(cancellationToken);

            return departments;
        }

        public async Task<EventResponseDto?> GetEventByIdAsync(
            Guid id,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            var e = await _dbContext.Events
                .AsNoTracking()
                .Include(ev => ev.JobVacancy)
                .FirstOrDefaultAsync(ev => ev.Id == id, cancellationToken);

            if (e == null) return null;

            // Security check
            if (companyId.HasValue && e.CompanyId.HasValue && e.CompanyId.Value != companyId.Value && e.CreatedBy != hrManagerId)
            {
                return null;
            }

            string? creatorName = null;
            var u = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(usr => usr.Id == e.CreatedBy, cancellationToken);
            if (u != null) creatorName = $"{u.FirstName} {u.LastName}".Trim();
            else
            {
                var c = await _dbContext.Companies.AsNoTracking().FirstOrDefaultAsync(cmp => cmp.Id == e.CreatedBy, cancellationToken);
                if (c != null) creatorName = c.CompanyName;
            }

            return MapToDto(e, creatorName);
        }

        public async Task<EventResponseDto?> UpdateEventAsync(
            Guid id,
            CreateEventDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var ev = await _dbContext.Events
                .Include(e => e.JobVacancy)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (ev == null) return null;

            // Security check: ensure authorized company or creator
            if (companyId.HasValue && ev.CompanyId.HasValue && ev.CompanyId.Value != companyId.Value && ev.CreatedBy != hrManagerId)
            {
                return null;
            }

            ev.Title = dto.Title.Trim();
            ev.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            ev.EventDate = dto.EventDate;
            ev.EventTime = dto.EventTime.Trim();
            if (dto.JobVacancyId.HasValue)
            {
                ev.JobVacancyId = dto.JobVacancyId.Value != Guid.Empty ? dto.JobVacancyId : null;
            }
            if (!string.IsNullOrWhiteSpace(dto.Department))
            {
                ev.Department = dto.Department.Trim();
            }
            else if (ev.JobVacancyId.HasValue && string.IsNullOrWhiteSpace(ev.Department))
            {
                var vacancy = await _dbContext.JobVacancies.AsNoTracking().FirstOrDefaultAsync(j => j.Id == ev.JobVacancyId.Value, cancellationToken);
                if (vacancy != null && !string.IsNullOrWhiteSpace(vacancy.Department))
                {
                    ev.Department = vacancy.Department.Trim();
                }
            }
            ev.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[EventService] Event {EventId} ('{Title}') updated by HR user {HrId}",
                ev.Id, ev.Title, hrManagerId);

            string? creatorName = null;
            var u = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(usr => usr.Id == ev.CreatedBy, cancellationToken);
            if (u != null) creatorName = $"{u.FirstName} {u.LastName}".Trim();
            else
            {
                var c = await _dbContext.Companies.AsNoTracking().FirstOrDefaultAsync(cmp => cmp.Id == ev.CreatedBy, cancellationToken);
                if (c != null) creatorName = c.CompanyName;
            }

            return MapToDto(ev, creatorName);
        }

        public async Task<bool> DeleteEventAsync(
            Guid id,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            var e = await _dbContext.Events.FirstOrDefaultAsync(ev => ev.Id == id, cancellationToken);
            if (e == null) return false;

            // Security check
            if (companyId.HasValue && e.CompanyId.HasValue && e.CompanyId.Value != companyId.Value && e.CreatedBy != hrManagerId)
            {
                return false;
            }

            // If this is a candidate interview event (CandidateId != null), revert candidate status back to "Ready for Interview"
            if (e.CandidateId.HasValue)
            {
                var candidateId = e.CandidateId.Value;
                var vacancyId = e.JobVacancyId;

                var hasOtherInterviews = await _dbContext.Events
                    .AnyAsync(other => other.Id != e.Id && other.CandidateId == candidateId && (!vacancyId.HasValue || other.JobVacancyId == vacancyId), cancellationToken);

                if (!hasOtherInterviews)
                {
                    var subQuery = _dbContext.Submissions
                        .Where(s => s.CandidateId == candidateId && s.IsSelectedForInterview);
                    if (vacancyId.HasValue) subQuery = subQuery.Where(s => s.JobVacancyId == vacancyId.Value);

                    var subs = await subQuery.ToListAsync(cancellationToken);
                    foreach (var sub in subs)
                    {
                        sub.Status = "Ready for Interview";
                        sub.UpdatedAt = DateTime.UtcNow;
                    }

                    var appQuery = _dbContext.JobApplications
                        .Where(a => a.CandidateId == candidateId);
                    if (vacancyId.HasValue) appQuery = appQuery.Where(a => a.JobId == vacancyId.Value);

                    var apps = await appQuery.ToListAsync(cancellationToken);
                    foreach (var app in apps)
                    {
                        app.Status = "Ready for Interview";
                        app.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            _dbContext.Events.Remove(e);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[EventService] Event {EventId} ('{Title}') was deleted by HR user {HrId}. Candidate interview status reverted if applicable.",
                id, e.Title, hrManagerId);

            return true;
        }

        public async Task<EventResponseDto> ScheduleCandidateInterviewAsync(
            ScheduleCandidateInterviewDto dto,
            Guid hrManagerId,
            Guid? companyId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (!DateOnly.TryParse(dto.EventDate, out var eventDate))
            {
                throw new ArgumentException("Invalid interview date format. Please use 'YYYY-MM-DD'.");
            }

            var cleanStart = string.IsNullOrWhiteSpace(dto.StartTime) ? "09:00" : dto.StartTime.Trim();
            var cleanEnd = string.IsNullOrWhiteSpace(dto.EndTime) ? "09:30" : dto.EndTime.Trim();

            if (!TryParseMinutes(cleanStart, out var newStart) || !TryParseMinutes(cleanEnd, out var newEnd))
            {
                throw new ArgumentException("Invalid time format. Please provide valid start and end times.");
            }

            if (newStart >= newEnd)
            {
                throw new ArgumentException("End time must be after start time.");
            }

            // Check for schedule clashes against existing company/HR events on this date
            var existingDayEvents = await _dbContext.Events
                .AsNoTracking()
                .Where(ev => ev.EventDate == eventDate &&
                    (companyId.HasValue ? ev.CompanyId == companyId.Value : ev.CreatedBy == hrManagerId))
                .ToListAsync(cancellationToken);

            foreach (var ev in existingDayEvents)
            {
                if (dto.ExistingEventId.HasValue && ev.Id == dto.ExistingEventId.Value)
                {
                    continue; // Skip the event currently being rescheduled
                }

                if (TryParseTimeRange(ev.EventTime, out var exStart, out var exEnd))
                {
                    if (newStart < exEnd && newEnd > exStart)
                    {
                        throw new InvalidOperationException(
                            $"The selected time slot ({cleanStart} - {cleanEnd}) is already taken on {dto.EventDate} by '{ev.Title}' ({ev.EventTime}). Please choose another time or date.");
                    }
                }
            }

            // Retrieve candidate and vacancy details
            var candidateUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == dto.CandidateId, cancellationToken);
            var candidateName = candidateUser != null ? $"{candidateUser.FirstName} {candidateUser.LastName}".Trim() : "Candidate";
            var candidateEmail = candidateUser?.Email ?? string.Empty;

            var vacancy = await _dbContext.JobVacancies.AsNoTracking().FirstOrDefaultAsync(j => j.Id == dto.JobVacancyId, cancellationToken);
            var jobTitle = vacancy?.Title ?? "Interview";
            var department = vacancy?.Department;

            var meetingMode = string.IsNullOrWhiteSpace(dto.MeetingMode) ? "Online" : dto.MeetingMode.Trim();
            var location = string.IsNullOrWhiteSpace(dto.Location)
                ? (meetingMode == "Online" ? "Google Meet (Link will be provided)" : "Company Head Office")
                : dto.Location.Trim();

            Event targetEvent;

            if (dto.ExistingEventId.HasValue && dto.ExistingEventId.Value != Guid.Empty)
            {
                var existing = await _dbContext.Events.FirstOrDefaultAsync(ev => ev.Id == dto.ExistingEventId.Value, cancellationToken);
                if (existing == null)
                {
                    throw new KeyNotFoundException($"Existing event '{dto.ExistingEventId.Value}' was not found.");
                }

                existing.Title = $"Interview: {candidateName} - {jobTitle}";
                existing.Description = $"Candidate: {candidateName} ({candidateEmail})\nRole: {jobTitle}\nMode: {meetingMode}\nLocation: {location}{(string.IsNullOrWhiteSpace(dto.Notes) ? "" : $"\nNotes: {dto.Notes.Trim()}")}";
                existing.EventDate = eventDate;
                existing.EventTime = $"{cleanStart} - {cleanEnd}";
                existing.MeetingMode = meetingMode;
                existing.Location = location;
                existing.Department = department ?? existing.Department;
                existing.UpdatedAt = DateTime.UtcNow;

                targetEvent = existing;
            }
            else
            {
                targetEvent = new Event
                {
                    Id = Guid.NewGuid(),
                    Title = $"Interview: {candidateName} - {jobTitle}",
                    Description = $"Candidate: {candidateName} ({candidateEmail})\nRole: {jobTitle}\nMode: {meetingMode}\nLocation: {location}{(string.IsNullOrWhiteSpace(dto.Notes) ? "" : $"\nNotes: {dto.Notes.Trim()}")}",
                    EventDate = eventDate,
                    EventTime = $"{cleanStart} - {cleanEnd}",
                    CreatedBy = hrManagerId,
                    CompanyId = companyId,
                    JobVacancyId = dto.JobVacancyId,
                    Department = department,
                    CandidateId = dto.CandidateId,
                    MeetingMode = meetingMode,
                    Location = location,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Events.Add(targetEvent);
            }

            // Update candidate submission status to "Selected"
            var subQuery = _dbContext.Submissions
                .Where(s => s.CandidateId == dto.CandidateId && s.IsSelectedForInterview);
            if (dto.JobVacancyId != Guid.Empty)
            {
                subQuery = subQuery.Where(s => s.JobVacancyId == dto.JobVacancyId);
            }
            var submissions = await subQuery.ToListAsync(cancellationToken);
            foreach (var s in submissions)
            {
                s.Status = "Selected";
                s.UpdatedAt = DateTime.UtcNow;
            }

            // Update job application status to "Interview"
            var appQuery = _dbContext.JobApplications
                .Where(a => a.CandidateId == dto.CandidateId);
            if (dto.JobVacancyId != Guid.Empty)
            {
                appQuery = appQuery.Where(a => a.JobId == dto.JobVacancyId);
            }
            var apps = await appQuery.ToListAsync(cancellationToken);
            foreach (var a in apps)
            {
                a.Status = "Interview";
                a.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[EventService] Interview scheduled/rescheduled for candidate {CandidateId} on {Date} {Time} by HR {HrId}",
                dto.CandidateId, eventDate, targetEvent.EventTime, hrManagerId);

            return MapToDto(targetEvent, null);
        }

        private static bool TryParseMinutes(string timeStr, out int minutes)
        {
            minutes = 0;
            if (string.IsNullOrWhiteSpace(timeStr)) return false;
            timeStr = timeStr.Trim();
            if (DateTime.TryParse(timeStr, out var dt))
            {
                minutes = dt.Hour * 60 + dt.Minute;
                return true;
            }
            var parts = timeStr.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
            {
                minutes = h * 60 + m;
                return true;
            }
            return false;
        }

        private static bool TryParseTimeRange(string eventTime, out int startMin, out int endMin)
        {
            startMin = 0;
            endMin = 0;
            if (string.IsNullOrWhiteSpace(eventTime)) return false;

            var separators = new[] { " - ", "-", " to ", " – " };
            string[]? parts = null;
            foreach (var sep in separators)
            {
                if (eventTime.Contains(sep))
                {
                    parts = eventTime.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
                    break;
                }
            }

            if (parts != null && parts.Length >= 2)
            {
                if (TryParseMinutes(parts[0], out startMin) && TryParseMinutes(parts[1], out endMin))
                {
                    return true;
                }
            }
            else if (TryParseMinutes(eventTime, out startMin))
            {
                endMin = startMin + 30;
                return true;
            }

            return false;
        }

        private static EventResponseDto MapToDto(Event e, string? creatorName)
        {
            return new EventResponseDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate.ToString("yyyy-MM-dd"),
                EventTime = e.EventTime,
                CreatedBy = e.CreatedBy,
                CreatorName = string.IsNullOrWhiteSpace(creatorName) ? null : creatorName,
                JobVacancyId = e.JobVacancyId,
                JobVacancyTitle = e.JobVacancy?.Title,
                Department = e.Department ?? e.JobVacancy?.Department,
                CandidateId = e.CandidateId,
                MeetingMode = e.MeetingMode,
                Location = e.Location,
                CreatedAt = e.CreatedAt
            };
        }
    }
}

