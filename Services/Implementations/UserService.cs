using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Users;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<UserResponseDto>> GetUsersByCompanyIdAsync(Guid companyId)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == companyId)
                .Include(u => u.Company)
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserResponseDto
                {
                    Id = u.Id,
                    CompanyId = u.CompanyId,
                    CompanyName = u.Company != null ? u.Company.CompanyName : string.Empty,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return null;
            }

            return new UserResponseDto
            {
                Id = user.Id,
                CompanyId = user.CompanyId,
                CompanyName = user.Company != null ? user.Company.CompanyName : string.Empty,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        /// <summary>
        /// Directly and completely deletes the user from the database.
        /// No soft-delete or block flag is used, ensuring clean lifecycle management.
        /// </summary>
        public async Task<bool> DeleteUserDirectlyAsync(Guid userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            // Direct physical removal from PostgreSQL
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
