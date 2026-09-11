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
    [Route("api/company")]
    [Produces("application/json")]
    public class CompanyAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CompanyAuthController> _logger;

        public CompanyAuthController(
            IAuthService authService,
            ApplicationDbContext dbContext,
            ILogger<CompanyAuthController> logger)
        {
            _authService = authService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new Enterprise Company entity (sole root account for employer login).
        /// Endpoint: POST /api/company/register
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterCompanyDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.RegisterCompanyAsync(dto);
                _logger.LogInformation("Successfully registered enterprise company '{CompanyName}' with email '{CompanyEmail}'", dto.CompanyName, dto.CompanyEmail);
                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("already registered"))
            {
                _logger.LogWarning("Registration conflict: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during company registration: {Message}", ex.Message);
                var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = detail });
            }
        }

        /// <summary>
        /// Authenticates an employer company directly against the Company table, returning an enterprise JWT token.
        /// Endpoint: POST /api/company/login
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _authService.LoginAsync(dto);
                _logger.LogInformation("Company '{Email}' successfully authenticated.", dto.Email);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Authentication failed for company '{Email}': {Message}", dto.Email, ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during login for company '{Email}': {Message}", dto.Email, ex.Message);
                var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = detail });
            }
        }

        /// <summary>
        /// Retrieves the profile and identity of the currently authenticated Company.
        /// Endpoint: GET /api/company/me
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCurrentCompany()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(companyIdClaim, out var companyId))
            {
                return Unauthorized(new { message = "Invalid or missing company identifier in token." });
            }

            var company = await _dbContext.Companies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound(new { message = "Company profile not found in database." });
            }

            var response = new UserResponseDto
            {
                Id = company.Id,
                CompanyId = company.Id,
                CompanyName = company.CompanyName,
                FullName = company.CompanyName,
                Email = company.ContactEmail,
                Role = "Company",
                CreatedAt = company.CreatedAt
            };

            return Ok(response);
        }
    }
}
