
namespace Skill_Hub_BackEnd.Services.Interviews
{
    public interface IHolidayService
    {
        Task<IReadOnlyList<NationalHolidayDto>> GetHolidaysAsync(
            int year,
            int? month = null,
            string? countryCode = "LK",
            CancellationToken cancellationToken = default);

        Task<HolidayConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);

        Task<HolidayConfigDto> UpdateApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default);
    }
}
