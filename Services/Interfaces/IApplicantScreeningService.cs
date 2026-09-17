using Skill_Hub_BackEnd.DTOs.Jobs;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IApplicantScreeningService
    {
        Task<IReadOnlyList<ScreenedApplicantDto>> FetchAndRankApplicantsAsync(
            Guid jobId,
            Guid companyId,
            CancellationToken cancellationToken = default);
    }
}
