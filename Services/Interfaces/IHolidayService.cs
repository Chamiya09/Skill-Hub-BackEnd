using Skill_Hub_BackEnd.DTOs.Events;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IHolidayService
    {
        Task<IReadOnlyList<NationalHolidayDto>> GetHolidaysAsync(
            int year,
            int? month = null,
            string? countryCode = "LK",
            CancellationToken cancellationToken = default);
    }
}
