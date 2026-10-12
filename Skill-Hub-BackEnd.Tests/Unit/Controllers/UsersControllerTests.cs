using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Skill_Hub_BackEnd.Controllers.Candidates;
using Skill_Hub_BackEnd.DTOs.Candidates;
using Skill_Hub_BackEnd.Services.Candidates;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class UsersControllerTests
    {
        private readonly Mock<IUserService> _userServiceMock;
        private readonly Mock<ILogger<UsersController>> _loggerMock;
        private readonly UsersController _controller;

        public UsersControllerTests()
        {
            _userServiceMock = new Mock<IUserService>();
            _loggerMock = new Mock<ILogger<UsersController>>();
            _controller = new UsersController(_userServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetUsersByCompany_ReturnsListOfUsers()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var expectedUsers = new List<UserResponseDto>
            {
                new UserResponseDto { Id = Guid.NewGuid(), FullName = "Alice Recruiter", Role = "Recruiter", CompanyId = companyId },
                new UserResponseDto { Id = Guid.NewGuid(), FullName = "Bob Manager", Role = "Hiring_Manager", CompanyId = companyId }
            };

            _userServiceMock.Setup(s => s.GetUsersByCompanyIdAsync(companyId)).ReturnsAsync(expectedUsers);

            // Act
            var result = await _controller.GetUsersByCompany(companyId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserResponseDto>>(okResult.Value);
            Assert.Equal(2, ((List<UserResponseDto>)users).Count);
        }

        [Fact]
        public async Task GetUserById_UserExists_ReturnsOkWithUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new UserResponseDto { Id = userId, FullName = "Jane Doe", Email = "jane@test.com" };
            _userServiceMock.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedUser = Assert.IsType<UserResponseDto>(okResult.Value);
            Assert.Equal(userId, returnedUser.Id);
        }

        [Fact]
        public async Task GetUserById_UserDoesNotExist_ReturnsNotFound404()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _userServiceMock.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync((UserResponseDto?)null);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteUser_UserExists_ReturnsNoContent204()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _userServiceMock.Setup(s => s.DeleteUserDirectlyAsync(userId)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteUser_UserNotFound_ReturnsNotFound404()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _userServiceMock.Setup(s => s.DeleteUserDirectlyAsync(userId)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
