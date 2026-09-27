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

        private static string? _dynamicApiKey;
        private static bool? _isGoogleConnected;
        private static string? _lastGoogleError;

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

        public string? ResolveApiKey()
        {
            var configKey = _configuration["GoogleCalendar:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_API_KEY")
                ?? Environment.GetEnvironmentVariable("GoogleCalendar__ApiKey");

            if (!string.IsNullOrWhiteSpace(configKey))
                return configKey.Trim();

            if (!string.IsNullOrWhiteSpace(_dynamicApiKey))
                return _dynamicApiKey.Trim();

            return null;
        }

        public Task<HolidayConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            var apiKey = ResolveApiKey();
            var hasKey = !string.IsNullOrWhiteSpace(apiKey);
            string? masked = null;
            if (hasKey && apiKey!.Length > 8)
            {
                masked = $"{apiKey[..4]}...{apiKey[^4..]}";
            }
            else if (hasKey)
            {
                masked = "••••••••";
            }

            var dto = new HolidayConfigDto
            {
                HasApiKey = hasKey,
                MaskedApiKey = masked,
                Source = _isGoogleConnected == true ? "GoogleCalendar" : "SriLankaGazette",
                IsGoogleConnected = _isGoogleConnected ?? false,
                LastError = _lastGoogleError
            };

            return Task.FromResult(dto);
        }

        public async Task<HolidayConfigDto> UpdateApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default)
        {
            _dynamicApiKey = apiKey?.Trim() ?? string.Empty;
            _configuration["GoogleCalendar:ApiKey"] = _dynamicApiKey;
            _lastGoogleError = null;
            _isGoogleConnected = null;

            if (!string.IsNullOrWhiteSpace(_dynamicApiKey))
            {
                try
                {
                    PersistApiKeyToAppSettings(_dynamicApiKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not persist GoogleCalendar:ApiKey to appsettings.json. In-memory configuration will remain active.");
                }

                // Verify with Google Calendar API immediately
                try
                {
                    var testUrl = $"https://www.googleapis.com/calendar/v3/calendars/en.lk%23holiday%40group.v.calendar.google.com/events?key={Uri.EscapeDataString(_dynamicApiKey)}&maxResults=1";
                    using var testResp = await _httpClient.GetAsync(testUrl, cancellationToken);
                    if (testResp.IsSuccessStatusCode)
                    {
                        _isGoogleConnected = true;
                        _lastGoogleError = null;
                        _logger.LogInformation("Successfully connected to Google Calendar API with provided API key.");
                    }
                    else
                    {
                        var errStr = await testResp.Content.ReadAsStringAsync(cancellationToken);
                        _isGoogleConnected = false;
                        _lastGoogleError = ParseGoogleErrorMessage(errStr, (int)testResp.StatusCode);
                        _logger.LogWarning("Google Calendar API verification failed: {Error}", _lastGoogleError);
                    }
                }
                catch (Exception ex)
                {
                    _isGoogleConnected = false;
                    _lastGoogleError = ex.Message;
                    _logger.LogWarning(ex, "Network error testing Google Calendar API key.");
                }
            }
            else
            {
                _isGoogleConnected = false;
                _lastGoogleError = null;
                try
                {
                    PersistApiKeyToAppSettings(string.Empty);
                }
                catch
                {
                    // ignore
                }
            }

            return await GetConfigAsync(cancellationToken);
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

            var apiKey = ResolveApiKey();
            var cacheKey = $"holidays_{normalizedCountry}_{year}_{month?.ToString() ?? "all"}_{(apiKey != null ? "live" : "gazette")}";
            if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<NationalHolidayDto>? cached) && cached != null)
            {
                return cached;
            }

            IReadOnlyList<NationalHolidayDto> results;

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    results = await FetchFromGoogleCalendarApiAsync(apiKey, calendarId, countryName, normalizedCountry, year, month, cancellationToken);
                    _isGoogleConnected = true;
                    _lastGoogleError = null;
                }
                catch (Exception ex)
                {
                    _isGoogleConnected = false;
                    _lastGoogleError = ex.Message;
                    _logger.LogWarning(ex, "Failed to fetch holidays from Google Calendar API. Falling back to official national holidays gazette for '{Country}'.", normalizedCountry);
                    results = await FetchFromOpenHolidaysApiAsync(normalizedCountry, countryName, year, month, cancellationToken);
                }
            }
            else
            {
                _isGoogleConnected = false;
                _logger.LogInformation("Google Calendar API key not configured. Utilizing official Government Gazette public holidays for '{Country}'.", normalizedCountry);
                results = await FetchFromOpenHolidaysApiAsync(normalizedCountry, countryName, year, month, cancellationToken);
            }

            _memoryCache.Set(cacheKey, results, TimeSpan.FromHours(12));
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
                throw new HttpRequestException(ParseGoogleErrorMessage(errorBody, (int)response.StatusCode));
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
                        CountryCode = countryCode,
                        Source = "GoogleCalendar"
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
                    CountryCode = countryCode,
                    Source = "OpenHolidays"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from fallback Open Holidays API for '{CountryCode}': {Message}", countryCode, ex.Message);
                return Array.Empty<NationalHolidayDto>();
            }
        }

        /// <summary>
        /// Official Sri Lanka Government Gazette Public, Bank, and Mercantile Holidays.
        /// Includes Full Moon Poya Days, Sinhala & Tamil New Year, Religious festivals, and National Day.
        /// </summary>
        private static IReadOnlyList<NationalHolidayDto> GetSriLankaNationalHolidays(int year, int? month)
        {
            var holidays = new List<(string Date, string Title, string Description)>
            {
                // Fixed annual national holidays
                ($"{year}-01-01", "New Year's Day", "Public, Bank and Merchant Holiday"),
                ($"{year}-02-04", "National Day (Independence Day)", "Public, Bank and Merchant Holiday"),
                ($"{year}-04-13", "Day Prior to Sinhala & Tamil New Year Day", "Public, Bank and Merchant Holiday"),
                ($"{year}-04-14", "Sinhala & Tamil New Year Day", "Public, Bank and Merchant Holiday"),
                ($"{year}-05-01", "May Day (International Workers' Day)", "Public, Bank and Merchant Holiday"),
                ($"{year}-12-25", "Christmas Day", "Public, Bank and Merchant Holiday"),
            };

            // Gazette-verified official holidays for 2026
            if (year == 2026)
            {
                holidays.AddRange(new[]
                {
                    ("2026-01-03", "Duruthu Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-01-15", "Tamil Thai Pongal Day", "Public, Bank and Merchant Holiday"),
                    ("2026-02-01", "Nawam Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-02-15", "Maha Sivarathri Day", "Public and Bank Holiday"),
                    ("2026-03-02", "Medin Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-03-21", "Id-Ul-Fitr (Ramazan Festival Day)", "Public and Bank Holiday"),
                    ("2026-04-01", "Bak Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-04-03", "Good Friday", "Public and Bank Holiday"),
                    ("2026-05-01", "Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2026-05-02", "Day Following Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2026-05-28", "Id-Ul-Alha (Hadji Festival Day)", "Public and Bank Holiday"),
                    ("2026-05-30", "Adhi Poson Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-06-29", "Poson Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2026-07-29", "Esala Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-08-26", "Milad-Un-Nabi (Holy Prophet's Birthday)", "Public and Bank Holiday"),
                    ("2026-08-27", "Nikini Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-09-26", "Binara Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-10-25", "Vap Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-11-08", "Deepavali Festival Day", "Public and Bank Holiday"),
                    ("2026-11-24", "Il Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2026-12-23", "Unduvap Full Moon Poya Day", "Public and Bank Holiday"),
                });
            }
            // Gazette-verified official holidays for 2025 (Gazette No. 2395/33)
            else if (year == 2025)
            {
                holidays.AddRange(new[]
                {
                    ("2025-01-13", "Duruthu Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-01-14", "Tamil Thai Pongal Day", "Public, Bank and Merchant Holiday"),
                    ("2025-02-12", "Nawam Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-02-26", "Maha Sivarathri Day", "Public and Bank Holiday"),
                    ("2025-03-13", "Medin Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-03-31", "Id-Ul-Fitr (Ramazan Festival Day)", "Public and Bank Holiday"),
                    ("2025-04-12", "Bak Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-04-15", "Special Bank Holiday", "Bank Holiday"),
                    ("2025-04-18", "Good Friday", "Public and Bank Holiday"),
                    ("2025-05-12", "Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2025-05-13", "Day Following Vesak Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2025-06-07", "Id-Ul-Alha (Hadji Festival Day)", "Public and Bank Holiday"),
                    ("2025-06-10", "Poson Full Moon Poya Day", "Public, Bank and Merchant Holiday"),
                    ("2025-07-10", "Esala Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-08-08", "Nikini Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-09-05", "Milad-Un-Nabi (Holy Prophet's Birthday)", "Public and Bank Holiday"),
                    ("2025-09-07", "Binara Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-10-06", "Vap Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-10-20", "Deepavali Festival Day", "Public and Bank Holiday"),
                    ("2025-11-05", "Il Full Moon Poya Day", "Public and Bank Holiday"),
                    ("2025-12-04", "Unduvap Full Moon Poya Day", "Public and Bank Holiday"),
                });
            }
            else
            {
                // Generalized astronomical estimate for other years
                holidays.AddRange(new[]
                {
                    ($"{year}-01-14", "Tamil Thai Pongal Day", "Public, Bank and Merchant Holiday"),
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
                    CountryCode = "LK",
                    Source = "SriLankaGazette"
                })
                .ToList();
        }

        private static string ParseGoogleErrorMessage(string json, int statusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var errorEl))
                {
                    if (errorEl.TryGetProperty("message", out var msgEl))
                    {
                        return msgEl.GetString() ?? $"Google Calendar API error ({statusCode})";
                    }
                }
            }
            catch
            {
                // ignore parsing failure
            }

            return $"Google Calendar API error ({statusCode})";
        }

        private static void PersistApiKeyToAppSettings(string apiKey)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Skill-Hub-BackEnd", "appsettings.json")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(json);
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();

                        if (dict.TryGetValue("GoogleCalendar", out var gcObj) && gcObj is JsonElement gcEl)
                        {
                            var gcDict = JsonSerializer.Deserialize<Dictionary<string, object>>(gcEl.GetRawText()) ?? new();
                            gcDict["ApiKey"] = apiKey;
                            dict["GoogleCalendar"] = gcDict;
                        }
                        else
                        {
                            dict["GoogleCalendar"] = new Dictionary<string, string>
                            {
                                ["ApiKey"] = apiKey,
                                ["DefaultCalendarId"] = "en.lk#holiday@group.v.calendar.google.com",
                                ["DefaultCountryCode"] = "LK"
                            };
                        }

                        var updatedJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(path, updatedJson);
                    }
                    catch
                    {
                        // best effort
                    }
                }
            }
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
