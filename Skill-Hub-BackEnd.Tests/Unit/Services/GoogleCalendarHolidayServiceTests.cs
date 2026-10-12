using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Services.Interviews;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Services
{
    public class GoogleCalendarHolidayServiceTests
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<ILogger<GoogleCalendarHolidayService>> _loggerMock;

        public GoogleCalendarHolidayServiceTests()
        {
            _httpClient = new HttpClient();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = new Mock<ILogger<GoogleCalendarHolidayService>>();
        }

        [Fact]
        public async Task GetHolidaysAsync_NoApiKeyProvided_FallsBackToGazetteHolidays()
        {
            // Arrange: empty configuration, no Google API Key
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = new GoogleCalendarHolidayService(_httpClient, emptyConfig, _memoryCache, _loggerMock.Object);

            // Act: Request Sri Lanka holidays for 2026
            var holidays = await service.GetHolidaysAsync(2026, null, "LK", CancellationToken.None);

            // Assert
            Assert.NotNull(holidays);
            Assert.NotEmpty(holidays);

            // Verify statutory Sri Lanka holidays are returned (e.g. Sinhala & Tamil New Year, Independence Day, Vesak)
            Assert.Contains(holidays, h => h.Title.Contains("Independence") || h.Title.Contains("New Year") || h.Title.Contains("Vesak") || h.Title.Contains("Poya"));
        }

        [Fact]
        public async Task GetConfigAsync_NoApiKey_ReturnsDefaultFallbackConfig()
        {
            // Arrange
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = new GoogleCalendarHolidayService(_httpClient, emptyConfig, _memoryCache, _loggerMock.Object);

            // Act
            var config = await service.GetConfigAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(config);
            Assert.False(config.HasApiKey);
            Assert.Null(config.MaskedApiKey);
            Assert.Equal("SriLankaGazette", config.Source);
        }

        [Fact]
        public async Task GetConfigAsync_WithConfiguredApiKey_ReturnsMaskedKey()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "GoogleCalendar:ApiKey", "AIzaSyD-SecretGoogleCalendarKey12345" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var service = new GoogleCalendarHolidayService(_httpClient, config, _memoryCache, _loggerMock.Object);

            // Act
            var holidayConfig = await service.GetConfigAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(holidayConfig);
            Assert.True(holidayConfig.HasApiKey);
            Assert.NotNull(holidayConfig.MaskedApiKey);
            Assert.StartsWith("AIza", holidayConfig.MaskedApiKey);
            Assert.EndsWith("2345", holidayConfig.MaskedApiKey);
            Assert.Contains("...", holidayConfig.MaskedApiKey);
        }

        [Fact]
        public async Task GetHolidaysAsync_RepeatedCalls_ReturnsCachedResults()
        {
            // Arrange
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = new GoogleCalendarHolidayService(_httpClient, emptyConfig, _memoryCache, _loggerMock.Object);

            // Act
            var firstCall = await service.GetHolidaysAsync(2026, 4, "LK", CancellationToken.None);
            var secondCall = await service.GetHolidaysAsync(2026, 4, "LK", CancellationToken.None);

            // Assert
            Assert.Same(firstCall, secondCall); // Same cached reference
        }
    }
}
