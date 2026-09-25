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

            _dbContext.Events.Remove(e);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[EventService] Event {EventId} ('{Title}') was deleted by HR user {HrId}",
                id, e.Title, hrManagerId);

            return true;
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
                CreatedAt = e.CreatedAt
            };
        }
    }
}

