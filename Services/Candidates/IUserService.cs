
namespace Skill_Hub_BackEnd.Services.Candidates
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponseDto>> GetUsersByCompanyIdAsync(Guid companyId);
        Task<UserResponseDto?> GetUserByIdAsync(Guid userId);
        Task<bool> DeleteUserDirectlyAsync(Guid userId);
    }
}
