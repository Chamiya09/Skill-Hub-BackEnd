using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Auth;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/company")]
    [Produces("application/json")]
    public class CompanyAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<CompanyAuthController> _logger;

        public CompanyAuthController(IAuthService authService, ILogger<CompanyAuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new Enterprise Company and provisions the primary HR Admin user account.
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
                _logger.LogInformation("Successfully registered company '{CompanyName}' with email '{CompanyEmail}'", dto.CompanyName, dto.CompanyEmail);
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
        /// Authenticates an employer/company user with email and password, returning an enterprise JWT token.
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
                _logger.LogInformation("Company user '{Email}' successfully authenticated.", dto.Email);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Authentication failed for company user '{Email}': {Message}", dto.Email, ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during login for company user '{Email}': {Message}", dto.Email, ex.Message);
                var detail = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = detail });
            }
        }
    }
}
