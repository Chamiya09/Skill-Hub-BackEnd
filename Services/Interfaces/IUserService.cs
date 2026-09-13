using Skill_Hub_BackEnd.DTOs.Users;

namespace Skill_Hub_BackEnd.Services.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponseDto>> GetUsersByCompanyIdAsync(Guid companyId);
        Task<UserResponseDto?> GetUserByIdAsync(Guid userId);
        Task<bool> DeleteUserDirectlyAsync(Guid userId);
    }
}
