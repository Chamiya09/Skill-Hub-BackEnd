using Skill_Hub_BackEnd.DTOs.Ai;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IMatchService
    {
        Task<AiMatchResponseDto> AnalyzeAsync(
            Guid candidateId,
            Guid jobId,
            CancellationToken cancellationToken = default);
    }
}
