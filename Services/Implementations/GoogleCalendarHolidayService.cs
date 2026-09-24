using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Skill_Hub_BackEnd.DTOs.Events;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class GoogleCalendarHolidayService : IHolidayService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<GoogleCalendarHolidayService> _logger;

        private static readonly Dictionary<string, (string CalendarId, string CountryName)> CountryCalendarMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["LK"] = ("en.lk#holiday@group.v.calendar.google.com", "Sri Lanka"),
                ["US"] = ("en.usa#holiday@group.v.calendar.google.com", "United States"),
                ["IN"] = ("en.indian#holiday@group.v.calendar.google.com", "India"),
                ["GB"] = ("en.uk#holiday@group.v.calendar.google.com", "United Kingdom"),
                ["UK"] = ("en.uk#holiday@group.v.calendar.google.com", "United Kingdom"),
                ["AU"] = ("en.australian#holiday@group.v.calendar.google.com", "Australia"),
                ["CA"] = ("en.canadian#holiday@group.v.calendar.google.com", "Canada"),
                ["SG"] = ("en.singapore#holiday@group.v.calendar.google.com", "Singapore")
            };

        public GoogleCalendarHolidayService(
            HttpClient httpClient,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            ILogger<GoogleCalendarHolidayService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<NationalHolidayDto>> GetHolidaysAsync(
            int year,
            int? month = null,
            string? countryCode = "LK",
            CancellationToken cancellationToken = default)
        {
            var normalizedCountry = string.IsNullOrWhiteSpace(countryCode) ? "LK" : countryCode.Trim().ToUpperInvariant();
            var (calendarId, countryName) = CountryCalendarMap.TryGetValue(normalizedCountry, out var mapped)
                ? mapped
                : ("en.lk#holiday@group.v.calendar.google.com", "Sri Lanka");

            var cacheKey = $"holidays_{normalizedCountry}_{year}_{month?.ToString() ?? "all"}";
            if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<NationalHolidayDto>? cached) && cached != null)
            {
                return cached;
            }

            var apiKey = _configuration["GoogleCalendar:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_API_KEY")
                ?? Environment.GetEnvironmentVariable("GoogleCalendar__ApiKey");

            IReadOnlyList<NationalHolidayDto> results;

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    results = await FetchFromGoogleCalendarApiAsync(apiKey, calendarId, countryName, normalizedCountry, year, month, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch holidays from Google Calendar API. Falling back to open national holidays service.");
                    results = await FetchFromOpenHolidaysApiAsync(normalizedCountry, countryName, year, month, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("Google Calendar API key not configured in appsettings/env. Utilizing high-availability Open Public Holidays service for '{Country}'.", normalizedCountry);
                results = await FetchFromOpenHolidaysApiAsync(normalizedCountry, countryName, year, month, cancellationToken);
            }

            _memoryCache.Set(cacheKey, results, TimeSpan.FromHours(24));
            return results;
        }

        private async Task<IReadOnlyList<NationalHolidayDto>> FetchFromGoogleCalendarApiAsync(
            string apiKey,
            string calendarId,
            string countryName,
            string countryCode,
            int year,
            int? month,
            CancellationToken cancellationToken)
        {
            DateTime startUtc;
            DateTime endUtc;

            if (month.HasValue && month.Value >= 1 && month.Value <= 12)
            {
                startUtc = new DateTime(year, month.Value, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-7);
                var daysInMonth = DateTime.DaysInMonth(year, month.Value);
                endUtc = new DateTime(year, month.Value, daysInMonth, 23, 59, 59, DateTimeKind.Utc).AddDays(8);
            }
            else
            {
                startUtc = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                endUtc = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            }

            var encodedCalendarId = Uri.EscapeDataString(calendarId);
            var url = $"https://www.googleapis.com/calendar/v3/calendars/{encodedCalendarId}/events" +
                      $"?key={Uri.EscapeDataString(apiKey)}" +
                      $"&timeMin={Uri.EscapeDataString(startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))}" +
                      $"&timeMax={Uri.EscapeDataString(endUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))}" +
                      $"&singleEvents=true" +
                      $"&orderBy=startTime";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Google Calendar API returned status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Google Calendar API returned status code {response.StatusCode}");
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            var list = new List<NationalHolidayDto>();

            if (root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var summary = item.TryGetProperty("summary", out var s) ? s.GetString() : null;
                    if (string.IsNullOrWhiteSpace(summary)) continue;

                    string? dateStr = null;
                    if (item.TryGetProperty("start", out var startEl))
                    {
                        if (startEl.TryGetProperty("date", out var dEl))
                        {
                            dateStr = dEl.GetString();
                        }
                        else if (startEl.TryGetProperty("dateTime", out var dtEl))
                        {
                            var dtStr = dtEl.GetString();
                            if (!string.IsNullOrEmpty(dtStr) && dtStr.Length >= 10)
                            {
                                dateStr = dtStr[..10];
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(dateStr)) continue;

                    var description = item.TryGetProperty("description", out var descEl) ? descEl.GetString() : "National / Public Holiday";
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();

                    list.Add(new NationalHolidayDto
                    {
                        Id = id,
                        Title = summary.Trim(),
                        Description = description,
                        Date = dateStr,
                        Country = countryName,
                        CountryCode = countryCode
                    });
                }
            }

            return list;
        }

        private async Task<IReadOnlyList<NationalHolidayDto>> FetchFromOpenHolidaysApiAsync(
            string countryCode,
            string countryName,
            int year,
            int? month,
            CancellationToken cancellationToken)
        {
            if (string.Equals(countryCode, "LK", StringComparison.OrdinalIgnoreCase))
            {
                return GetSriLankaNationalHolidays(year, month);
            }

            var url = $"https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}";
            try
            {
                var holidays = await _httpClient.GetFromJsonAsync<List<NagerHolidayItem>>(url, cancellationToken);
                if (holidays == null || holidays.Count == 0)
                {
                    return Array.Empty<NationalHolidayDto>();
                }

                var filtered = holidays.AsEnumerable();
                if (month.HasValue)
                {
                    var monthStr = $"-{month.Value:D2}-";
                    filtered = filtered.Where(h => h.Date != null && h.Date.Contains(monthStr));
                }

                return filtered.Select(h => new NationalHolidayDto
                {
                    Id = $"{h.Date}_{h.Name ?? h.LocalName}",
                    Title = h.LocalName ?? h.Name ?? "National Holiday",
                    Description = h.Name != h.LocalName ? $"{h.Name} (Public Holiday)" : "Public & National Holiday",
                    Date = h.Date ?? string.Empty,
                    Country = countryName,
                    CountryCode = countryCode
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from fallback Open Holidays API for '{CountryCode}': {Message}", countryCode, ex.Message);
                return Array.Empty<NationalHolidayDto>();
            }
        }

        private static IReadOnlyList<NationalHolidayDto> GetSriLankaNationalHolidays(int year, int? month)
        {
            var holidays = new List<(string Date, string Title, string Description)>
            {
                // Fixed annual national holidays
                ($"{year}-01-01", "New Year's Day", "Public, Bank and Merchant Holiday"),
                ($"{year}-01-14", "Tamil Thai Pongal Day", "Public, Bank and Merchant Holiday"),
                ($"{year}-02-04", "National Day (Independence Day)", "Public, Bank and Merchant Holiday"),
                ($"{year}-04-13", "Day Prior to Sinhala & Tamil New Year", "Public, Bank and Merchant Holiday"),
                ($"{year}-04-14", "Sinhala & Tamil New Year Day", "Public, Bank and Merchant Holiday"),
                ($"{year}-05-01", "May Day (International Workers' Day)", "Public, Bank and Merchant Holiday"),
                ($"{year}-12-25", "Christmas Day", "Public, Bank and Merchant Holiday"),
            };

            // Lunar Poya days and Islamic/Hindu religious festivals for 2026
            if (year == 2026)
            {
                holidays.AddRange(new[]
                {
                    ("2026-01-24", "Duruthu Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-02-22", "Navam Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-03-20", "Id Ul-Fitr (Ramazan Festival Day)", "Public and Bank Holiday"),
                    ("2026-03-23", "Medin Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-04-03", "Good Friday", "Public and Bank Holiday"),
                    ("2026-04-21", "Bak Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-05-20", "Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2026-05-21", "Day Following Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2026-05-27", "Id Ul-Alha (Hadji Festival Day)", "Public and Bank Holiday"),
                    ("2026-06-19", "Poson Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2026-07-18", "Esala Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-08-17", "Nikini Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-08-25", "Milad-Un-Nabi (Holy Prophet's Birthday)", "Public and Bank Holiday"),
                    ("2026-09-16", "Binara Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-10-15", "Vap Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-11-08", "Deepavali (Festival of Lights)", "Public and Bank Holiday"),
                    ("2026-11-14", "Il Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-12-13", "Unduvap Full Moon Poya Day", "Public and Bank Holiday"),
                });
            }
            else if (year == 2025)
            {
                holidays.AddRange(new[]
                {
                    ("2025-01-13", "Duruthu Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-02-12", "Navam Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-02-26", "Maha Sivarathri Day", "Public and Bank Holiday"),
                    ("2025-03-13", "Medin Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-03-31", "Id Ul-Fitr (Ramazan Festival Day)", "Public and Bank Holiday"),
                    ("2025-04-12", "Bak Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-04-18", "Good Friday", "Public and Bank Holiday"),
                    ("2025-05-12", "Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2025-05-13", "Day Following Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2025-06-07", "Id Ul-Alha (Hadji Festival Day)", "Public and Bank Holiday"),
                    ("2025-06-10", "Poson Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2025-07-10", "Esala Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-08-08", "Nikini Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-09-05", "Milad-Un-Nabi (Holy Prophet's Birthday)", "Public and Bank Holiday"),
                    ("2025-09-07", "Binara Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-10-06", "Vap Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-10-20", "Deepavali (Festival of Lights)", "Public and Bank Holiday"),
                    ("2025-11-05", "Il Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-12-04", "Unduvap Full Moon Poya Day", "Public and Bank Holiday"),
                });
            }
            else
            {
                holidays.AddRange(new[]
                {
                    ($"{year}-01-15", "Duruthu Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-02-14", "Navam Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-03-15", "Medin Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-04-15", "Bak Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-05-15", "Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ($"{year}-05-16", "Day Following Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ($"{year}-06-15", "Poson Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ($"{year}-07-15", "Esala Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-08-15", "Nikini Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-09-15", "Binara Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-10-15", "Vap Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-11-15", "Il Full Moon Poya Day", "Public and Bank Holiday"),
                    ($"{year}-12-15", "Unduvap Full Moon Poya Day", "Public and Bank Holiday"),
                });
            }

            var query = holidays.AsEnumerable();
            if (month.HasValue && month.Value >= 1 && month.Value <= 12)
            {
                var monthPrefix = $"{year}-{month.Value:D2}-";
                query = query.Where(h => h.Date.StartsWith(monthPrefix, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderBy(h => h.Date)
                .Select(h => new NationalHolidayDto
                {
                    Id = $"{h.Date}_{h.Title}",
                    Title = h.Title,
                    Description = h.Description,
                    Date = h.Date,
                    Country = "Sri Lanka",
                    CountryCode = "LK"
                })
                .ToList();
        }

        private sealed class NagerHolidayItem
        {
            public string? Date { get; set; }
            public string? LocalName { get; set; }
            public string? Name { get; set; }
            public string? CountryCode { get; set; }
        }
    }
}
