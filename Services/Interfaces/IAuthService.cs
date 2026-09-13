using Skill_Hub_BackEnd.DTOs.Auth;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterCompanyAsync(RegisterCompanyDto dto);
        Task<AuthResponseDto> RegisterCandidateAsync(RegisterCandidateDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> LoginCandidateAsync(LoginDto dto);
        Task<AuthResponseDto> LoginGeneralAsync(LoginDto dto);
    }
}
