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
                    AdminName = company.AdminName ?? company.CompanyName,
                    FullName = company.AdminName ?? company.CompanyName,
                    Email = company.ContactEmail,
                    ContactEmail = company.ContactEmail,
                    Role = "Company",
                    Phone = company.Phone,
                    CompanySize = company.CompanySize,
                    FoundedYear = company.FoundedYear,
                    LogoUrl = company.LogoUrl,
                    Website = company.Website,
                    LinkedinUrl = company.LinkedinUrl,
                    TwitterUrl = company.TwitterUrl,
                    GithubUrl = company.GithubUrl,
                    Location = company.Location,
                    Industry = company.Industry,
                    About = company.About,
                    CreatedAt = company.CreatedAt,
                    UpdatedAt = company.UpdatedAt
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
                    AdminName = company.AdminName ?? company.CompanyName,
                    FullName = company.AdminName ?? company.CompanyName,
                    Email = company.ContactEmail,
                    ContactEmail = company.ContactEmail,
                    Role = "Company",
                    Phone = company.Phone,
                    CompanySize = company.CompanySize,
                    FoundedYear = company.FoundedYear,
                    LogoUrl = company.LogoUrl,
                    Website = company.Website,
                    LinkedinUrl = company.LinkedinUrl,
                    TwitterUrl = company.TwitterUrl,
                    GithubUrl = company.GithubUrl,
                    Location = company.Location,
                    Industry = company.Industry,
                    About = company.About,
                    CreatedAt = company.CreatedAt,
                    UpdatedAt = company.UpdatedAt
                }
            };
        }

        public async Task<AuthResponseDto> RegisterCandidateAsync(RegisterCandidateDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();
            var firstName = dto.FirstName.Trim();
            var lastName = dto.LastName.Trim();
            var fullName = $"{firstName} {lastName}".Trim();

            // Check if email already registered in Users or Companies table
            var userExists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            var companyExists = await _dbContext.Companies.AnyAsync(c => c.ContactEmail.ToLower() == normalizedEmail);

            if (userExists || companyExists)
            {
                throw new InvalidOperationException($"An account with the email '{dto.Email}' is already registered.");
            }

            // Securely hash password with BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Create Candidate User entity
            var candidate = new User
            {
                Id = Guid.NewGuid(),
                CompanyId = null,
                FirstName = firstName,
                LastName = lastName,
                FullName = fullName,
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                Role = "CANDIDATE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(candidate);
            await _dbContext.SaveChangesAsync();

            // Generate JWT Token with Candidate claims and role 'CANDIDATE'
            var (token, expiresAt) = _tokenService.GenerateToken(candidate, null);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = candidate.Id,
                    CompanyId = null,
                    CompanyName = string.Empty,
                    FirstName = candidate.FirstName,
                    LastName = candidate.LastName,
                    FullName = candidate.FullName,
                    Email = candidate.Email,
                    ContactEmail = candidate.Email,
                    Role = "CANDIDATE",
                    Headline = candidate.Headline,
                    Phone = candidate.Phone,
                    Location = candidate.Location,
                    AvatarUrl = candidate.AvatarUrl,
                    CreatedAt = candidate.CreatedAt,
                    UpdatedAt = candidate.UpdatedAt
                }
            };
        }

        public async Task<AuthResponseDto> LoginCandidateAsync(LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            // Find Candidate in Users table
            var candidate = await _dbContext.Users
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && (u.Role == "CANDIDATE" || u.Role == "Candidate"));

            if (candidate == null || !BCrypt.Net.BCrypt.Verify(dto.Password, candidate.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid email or password credentials.");
            }

            // Generate JWT Token
            var (token, expiresAt) = _tokenService.GenerateToken(candidate, candidate.Company?.CompanyName);

            return new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = new UserResponseDto
                {
                    Id = candidate.Id,
                    CompanyId = candidate.CompanyId,
                    CompanyName = candidate.Company?.CompanyName ?? string.Empty,
                    FirstName = candidate.FirstName,
                    LastName = candidate.LastName,
                    FullName = candidate.FullName,
                    Email = candidate.Email,
                    ContactEmail = candidate.Email,
                    Role = candidate.Role,
                    Headline = candidate.Headline,
                    Phone = candidate.Phone,
                    Location = candidate.Location,
                    AvatarUrl = candidate.AvatarUrl,
                    CreatedAt = candidate.CreatedAt,
                    UpdatedAt = candidate.UpdatedAt
                }
            };
        }

        public async Task<AuthResponseDto> LoginGeneralAsync(LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLower();

            // 1. Check if user is a Candidate or Team Member in Users table
            var user = await _dbContext.Users
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user != null && BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                var (token, expiresAt) = _tokenService.GenerateToken(user, user.Company?.CompanyName);
                return new AuthResponseDto
                {
                    Token = token,
                    TokenType = "Bearer",
                    ExpiresAt = expiresAt,
                    User = new UserResponseDto
                    {
                        Id = user.Id,
                        CompanyId = user.CompanyId,
                        CompanyName = user.Company?.CompanyName ?? string.Empty,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        FullName = user.FullName,
                        Email = user.Email,
                        ContactEmail = user.Email,
                        Role = user.Role,
                        Headline = user.Headline,
                        Phone = user.Phone,
                        Location = user.Location,
                        AvatarUrl = user.AvatarUrl,
                        CreatedAt = user.CreatedAt,
                        UpdatedAt = user.UpdatedAt
                    }
                };
            }

            // 2. Check if user is a Company account
            var company = await _dbContext.Companies
                .FirstOrDefaultAsync(c => c.ContactEmail.ToLower() == normalizedEmail);

            if (company != null && BCrypt.Net.BCrypt.Verify(dto.Password, company.PasswordHash))
            {
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
                        AdminName = company.AdminName ?? company.CompanyName,
                        FullName = company.AdminName ?? company.CompanyName,
                        Email = company.ContactEmail,
                        ContactEmail = company.ContactEmail,
                        Role = "Company",
                        Phone = company.Phone,
                        CompanySize = company.CompanySize,
                        FoundedYear = company.FoundedYear,
                        LogoUrl = company.LogoUrl,
                        Website = company.Website,
                        LinkedinUrl = company.LinkedinUrl,
                        TwitterUrl = company.TwitterUrl,
                        GithubUrl = company.GithubUrl,
                        Location = company.Location,
                        Industry = company.Industry,
                        About = company.About,
                        CreatedAt = company.CreatedAt,
                        UpdatedAt = company.UpdatedAt
                    }
                };
            }

            throw new UnauthorizedAccessException("Invalid email or password credentials.");
        }
    }
}
