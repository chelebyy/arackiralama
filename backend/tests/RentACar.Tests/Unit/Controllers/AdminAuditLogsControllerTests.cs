using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Audit;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;
using Microsoft.AspNetCore.Http;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminAuditLogsControllerTests
{
    [Fact]
    public async Task GetPaged_WithInvalidPage_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IAuditLogService>());

        var result = await controller.GetPaged(action: null, entityType: null, userId: null, fromUtc: null, toUtc: null, page: 0, cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPaged_WithInvalidPageSize_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IAuditLogService>());

        var result = await controller.GetPaged(action: null, entityType: null, userId: null, fromUtc: null, toUtc: null, pageSize: 0, cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPaged_WithTooLargePageSize_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IAuditLogService>());

        var result = await controller.GetPaged(action: null, entityType: null, userId: null, fromUtc: null, toUtc: null, pageSize: 201, cancellationToken: CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPaged_ReturnsAuditLogs()
    {
        var serviceMock = new Mock<IAuditLogService>();
        serviceMock.Setup(s => s.GetPagedAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLogListResponse(1, 1, 50, new List<AuditLogDto>
            {
                new(Guid.NewGuid(), "Create", "Reservation", Guid.NewGuid().ToString(), "admin-1", DateTime.UtcNow, null, null, null, "details")
            }));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetPaged(action: null, entityType: null, userId: null, fromUtc: null, toUtc: null, cancellationToken: CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<AuditLogListResponse>>().Subject;
        response.Success.Should().BeTrue();
    }

    private static AdminAuditLogsController CreateController(IAuditLogService service)
    {
        return new AdminAuditLogsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
