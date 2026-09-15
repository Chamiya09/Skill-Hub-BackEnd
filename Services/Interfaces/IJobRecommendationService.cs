using Skill_Hub_BackEnd.DTOs.Recommendations;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IJobRecommendationService
    {
        Task<IReadOnlyList<RecommendedJobResponseDto>> GetRecommendedJobsAsync(
            Guid candidateId,
            CancellationToken cancellationToken = default);
    }
}
