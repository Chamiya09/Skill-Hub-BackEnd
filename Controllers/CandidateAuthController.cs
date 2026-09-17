using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;
using Skill_Hub_BackEnd.DTOs.Auth;
using Skill_Hub_BackEnd.DTOs.Users;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/candidate")]
    [Route("api/auth/candidate")]
    [Produces("application/json")]
    public class CandidateAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CandidateAuthController> _logger;

        public CandidateAuthController(
            IAuthService authService,
            ApplicationDbContext dbContext,
            ILogger<CandidateAuthController> logger)
        {
            _authService = authService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new Candidate (Job Seeker) user with role CANDIDATE.
        /// Endpoints: POST /api/auth/candidate/register, POST /api/candidate/register
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterCandidate([FromBody] RegisterCandidateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.RegisterCandidateAsync(dto);
                _logger.LogInformation("Successfully registered candidate '{FullName}' with email '{Email}'", response.User.FullName, response.User.Email);
                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already registered") || ex.Message.Contains("already exists"))
            {
                _logger.LogWarning("Candidate registration conflict: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during candidate registration for '{Email}': {Message}", dto.Email, ex.Message);
                var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = detail });
            }
        }

        /// <summary>
        /// Authenticates a Candidate against the database, returning JWT token and candidate user profile with role CANDIDATE.
        /// Endpoints: POST /api/auth/candidate/login, POST /api/candidate/login
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> LoginCandidate([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.LoginCandidateAsync(dto);
                _logger.LogInformation("Candidate '{Email}' successfully authenticated.", dto.Email);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Candidate authentication failed for '{Email}': {Message}", dto.Email, ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during candidate login for '{Email}': {Message}", dto.Email, ex.Message);
                var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = detail });
            }
        }

        /// <summary>
        /// Universal login endpoint supporting both Candidates and Company accounts.
        /// Endpoint: POST /api/auth/login
        /// </summary>
        [HttpPost("/api/auth/login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UniversalLogin([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.LoginGeneralAsync(dto);
                _logger.LogInformation("User '{Email}' authenticated with role '{Role}'.", dto.Email, response.User.Role);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Universal login failed for '{Email}': {Message}", dto.Email, ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for '{Email}': {Message}", dto.Email, ex.Message);
                var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = detail });
            }
        }

        /// <summary>
        /// Retrieves the profile information for the authenticated Candidate.
        /// Endpoint: GET /api/candidate/me
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCurrentCandidate()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid or missing user identifier in token." });
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Candidate profile was not found in the database." });
            }

            var response = new UserResponseDto
            {
                Id = user.Id,
                CompanyId = user.CompanyId,
                CompanyName = string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Email = user.Email,
                ContactEmail = user.Email,
                Role = user.Role,
                Headline = user.Headline,
                Phone = user.Phone,
                Location = user.Location,
                Experience = user.Experience,
                Availability = user.Availability,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return Ok(response);
        }

        /// <summary>
        /// Updates the profile for the authenticated Candidate.
        /// Endpoint: PUT /api/candidate/profile
        /// </summary>
        [HttpPut("profile")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCandidateProfile([FromBody] UpdateCandidateProfileDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid or missing user identifier in token." });
            }

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Candidate profile was not found in the database." });
            }

            if (!string.IsNullOrWhiteSpace(dto.FirstName))
            {
                user.FirstName = dto.FirstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.LastName))
            {
                user.LastName = dto.LastName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
            {
                user.FullName = $"{user.FirstName} {user.LastName}".Trim();
            }

            if (dto.Headline != null)
            {
                user.Headline = dto.Headline.Trim();
            }

            if (dto.Phone != null)
            {
                user.Phone = dto.Phone.Trim();
            }

            if (dto.Location != null)
            {
                user.Location = dto.Location.Trim();
            }

            if (dto.Experience != null)
            {
                user.Experience = dto.Experience.Trim();
            }

            if (dto.Availability != null)
            {
                user.Availability = dto.Availability.Trim();
            }

            if (dto.AvatarUrl != null)
            {
                user.AvatarUrl = dto.AvatarUrl.Trim();
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var response = new UserResponseDto
            {
                Id = user.Id,
                CompanyId = user.CompanyId,
                CompanyName = string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Email = user.Email,
                ContactEmail = user.Email,
                Role = user.Role,
                Headline = user.Headline,
                Phone = user.Phone,
                Location = user.Location,
                Experience = user.Experience,
                Availability = user.Availability,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return Ok(response);
        }
    }

    public class UpdateCandidateProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Headline { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public string? Experience { get; set; }
        public string? Availability { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
