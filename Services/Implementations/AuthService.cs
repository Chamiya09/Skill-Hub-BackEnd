using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Auth;
using Skill_Hub_BackEnd.DTOs.Users;
using Skill_Hub_BackEnd.Models;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ITokenService _tokenService;

        public AuthService(ApplicationDbContext dbContext, ITokenService tokenService)
        {
            _dbContext = dbContext;
            _tokenService = tokenService;
        }

        public async Task<AuthResponseDto> RegisterCompanyAsync(RegisterCompanyDto dto)
        {
            // Check if admin email already exists in system
            var normalizedEmail = dto.AdminEmail.Trim().ToLower();
            var existingUser = await _dbContext.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (existingUser)
            {
                throw new InvalidOperationException($"A user with the email '{dto.AdminEmail}' already exists.");
            }

            // Create Company Entity
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = dto.CompanyName.Trim(),
                ContactEmail = dto.ContactEmail.Trim().ToLower(),
                Industry = dto.Industry?.Trim(),
                Website = dto.Website?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Hash password securely with BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Create First User as 'HR_Admin'
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                FullName = dto.AdminFullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                Role = "HR_Admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Execute in an atomic transaction
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                _dbContext.Companies.Add(company);
                _dbContext.Users.Add(adminUser);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Generate JWT Token
            var (token, expiresAt) = _tokenService.GenerateToken(adminUser, company.CompanyName);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = adminUser.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FullName = adminUser.FullName,
                    Email = adminUser.Email,
                    Role = adminUser.Role,
                    CreatedAt = adminUser.CreatedAt
                }
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            var user = await _dbContext.Users
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid email or password credentials.");
            }

            var companyName = user.Company?.CompanyName ?? "Skill Hub Enterprise";
            var (token, expiresAt) = _tokenService.GenerateToken(user, companyName);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    CompanyId = user.CompanyId,
                    CompanyName = companyName,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt
                }
            };
        }
    }
}
