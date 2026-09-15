using Skill_Hub_BackEnd.DTOs.Ai;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IAiAgentService
    {
        Task<AiMatchResponseDto> AnalyzeCandidateMatchAsync(AiMatchRequestDto request);
    }
}
