using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill_Hub_BackEnd.DTOs.Users;
using Skill_Hub_BackEnd.Services.Interfaces;

namespace Skill_Hub_BackEnd.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Route("api/v1/users")]
    [Authorize]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all team members / users registered under a specific company.
        /// </summary>
        [HttpGet("company/{companyId:guid}")]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsersByCompany([FromRoute] Guid companyId)
        {
            var users = await _userService.GetUsersByCompanyIdAsync(companyId);
            return Ok(users);
        }

        /// <summary>
        /// Retrieves detailed information for a single user by their unique ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById([FromRoute] Guid id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"User with ID '{id}' was not found." });
            }

            return Ok(user);
        }

        /// <summary>
        /// Directly and permanently deletes a user from the database.
        /// (Strictly implements direct physical deletion, without soft-blocks or status disabling).
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "HR_Admin,Super_Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
        {
            var isDeleted = await _userService.DeleteUserDirectlyAsync(id);
            if (!isDeleted)
            {
                _logger.LogWarning("Attempted to delete user '{UserId}', but user was not found.", id);
                return NotFound(new { message = $"User with ID '{id}' was not found or has already been deleted." });
            }

            _logger.LogInformation("User '{UserId}' was directly and permanently removed from the database.", id);
            return NoContent();
        }
    }
}
