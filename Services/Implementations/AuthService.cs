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

            // Check if company email already exists in Company table
            var existingCompany = await _dbContext.Companies
                .AnyAsync(c => c.ContactEmail.ToLower() == normalizedEmail);

            if (existingCompany)
            {
                throw new InvalidOperationException($"A company with the email '{dto.CompanyEmail}' is already registered.");
            }

            // Hash password securely with BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Create Company Entity (acts as the sole independent entity for employer login)
            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = companyName,
                ContactEmail = normalizedEmail,
                PasswordHash = passwordHash,
                Industry = dto.Industry?.Trim(),
                Website = dto.Website?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Atomically add only the Company entity (no User records created)
            _dbContext.Companies.Add(company);
            await _dbContext.SaveChangesAsync();

            // Generate JWT Token with CompanyId and Role 'Company'
            var (token, expiresAt) = _tokenService.GenerateToken(company);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = company.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FullName = company.CompanyName,
                    Email = company.ContactEmail,
                    Role = "Company",
                    CreatedAt = company.CreatedAt
                }
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            // Query strictly against the Company table
            var company = await _dbContext.Companies
                .FirstOrDefaultAsync(c => c.ContactEmail.ToLower() == normalizedEmail);

            if (company == null || !BCrypt.Net.BCrypt.Verify(dto.Password, company.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid company email or password credentials.");
            }

            // Generate JWT Token with CompanyId and Role 'Company'
            var (token, expiresAt) = _tokenService.GenerateToken(company);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = company.Id,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    FullName = company.CompanyName,
                    Email = company.ContactEmail,
                    Role = "Company",
                    CreatedAt = company.CreatedAt
                }
            };
        }
    }
}
