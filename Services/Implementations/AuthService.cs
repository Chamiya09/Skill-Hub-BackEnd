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
            var normalizedEmail = dto.CompanyEmail.Trim().ToLower();
            var companyName = dto.CompanyName.Trim();

            // Check if user or company email already exists
            var existingUser = await _dbContext.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            var existingCompany = await _dbContext.Companies
                .AnyAsync(c => c.ContactEmail.ToLower() == normalizedEmail);

            if (existingUser || existingCompany)
            {
                throw new InvalidOperationException($"A company with the email '{dto.CompanyEmail}' is already registered.");
            }

            // Hash password securely with BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Create Company Entity
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = companyName,
                ContactEmail = normalizedEmail,
                Industry = dto.Industry?.Trim(),
                Website = dto.Website?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create Root Company Account User
            var companyRootUser = new User
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                FullName = companyName,
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                Role = "HR_Admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Atomically add and save both entities
            _dbContext.Companies.Add(company);
            _dbContext.Users.Add(companyRootUser);
            await _dbContext.SaveChangesAsync();

            // Generate JWT Token for the company root account
            var (token, expiresAt) = _tokenService.GenerateToken(companyRootUser, company.CompanyName);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = companyRootUser.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FullName = companyRootUser.FullName,
                    Email = companyRootUser.Email,
                    Role = companyRootUser.Role,
                    CreatedAt = companyRootUser.CreatedAt
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
                throw new UnauthorizedAccessException("Invalid company email or password credentials.");
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
