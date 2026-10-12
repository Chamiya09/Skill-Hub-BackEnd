using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Skill_Hub_BackEnd.Controllers.Interviews;
using Skill_Hub_BackEnd.DTOs.Interviews;
using Skill_Hub_BackEnd.Services.Interviews;
using Skill_Hub_BackEnd.Tests.Fixtures;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Controllers
{
    public class EventsControllerTests
    {
        private readonly Mock<IEventService> _eventServiceMock;
        private readonly Mock<IHolidayService> _holidayServiceMock;
        private readonly Mock<IInterviewSchedulerService> _schedulerServiceMock;

        public EventsControllerTests()
        {
            _eventServiceMock = new Mock<IEventService>();
            _holidayServiceMock = new Mock<IHolidayService>();
            _schedulerServiceMock = new Mock<IInterviewSchedulerService>();
        }

        private EventsController CreateController(Guid userId, Guid? companyId = null)
        {
            var controller = new EventsController(
                _eventServiceMock.Object,
                _holidayServiceMock.Object,
                _schedulerServiceMock.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = AuthTokenTestHelper.CreateClaimsPrincipal(userId, "recruiter@company.test", "HR_Admin", companyId: companyId)
                }
            };
            return controller;
        }

        [Fact]
        public async Task CreateEvent_ValidDto_ReturnsCreatedAtAction201()
        {
            // Arrange
            var hrManagerId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, companyId);

            var dto = new CreateEventDto
            {
                Title = "Technical Round 1: Algorithms",
                EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                EventTime = "10:00 - 11:00",
                Department = "Engineering",
                Description = "Round 1 Live Coding Interview"
            };

            var eventResponse = new EventResponseDto
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                EventDate = dto.EventDate.ToString("yyyy-MM-dd"),
                EventTime = dto.EventTime,
                Department = dto.Department,
                Description = dto.Description,
                MeetingMode = "Online",
                Location = "https://meet.google.com/abc-defg-hij"
            };

            _eventServiceMock.Setup(s => s.CreateEventAsync(dto, hrManagerId, companyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(eventResponse);

            // Act
            var result = await controller.CreateEvent(dto, CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(eventResponse, createdResult.Value);
        }

        [Fact]
        public async Task GetEvents_WithFilters_ReturnsFilteredEventList()
        {
            // Arrange
            var hrManagerId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, companyId);

            var eventList = new List<EventResponseDto>
            {
                new EventResponseDto { Id = Guid.NewGuid(), Title = "Interview A", Department = "Engineering" },
                new EventResponseDto { Id = Guid.NewGuid(), Title = "Interview B", Department = "Engineering" }
            };

            _eventServiceMock.Setup(s => s.GetEventsAsync(
                companyId,
                hrManagerId,
                2026,
                10,
                null,
                null,
                "Engineering",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(eventList);

            // Act
            var result = await controller.GetEvents(2026, 10, null, null, "Engineering", CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsAssignableFrom<IReadOnlyList<EventResponseDto>>(okResult.Value);
            Assert.Equal(2, returned.Count);
        }

        [Fact]
        public async Task GetEventById_EventExists_ReturnsOk200()
        {
            // Arrange
            var hrManagerId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, companyId);

            var eventResponse = new EventResponseDto { Id = eventId, Title = "Found Event" };
            _eventServiceMock.Setup(s => s.GetEventByIdAsync(eventId, hrManagerId, companyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(eventResponse);

            // Act
            var result = await controller.GetEventById(eventId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(eventResponse, okResult.Value);
        }

        [Fact]
        public async Task DeleteEvent_EventExists_ReturnsNoContent204()
        {
            // Arrange
            var hrManagerId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var controller = CreateController(hrManagerId, companyId);

            _eventServiceMock.Setup(s => s.DeleteEventAsync(eventId, hrManagerId, companyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await controller.DeleteEvent(eventId, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }
}
