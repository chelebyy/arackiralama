using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Reports;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminReportsControllerTests
{
    [Fact]
    public async Task GetRevenue_WithValidPeriod_ReturnsOk()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetRevenueReportAsync("monthly", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueReportResponse(
                "monthly",
                5000m,
                10,
                500m,
                new List<RevenueReportBreakdownItemResponse>
                {
                    new(new DateOnly(2030, 6, 1), 2500m, 5),
                    new(new DateOnly(2030, 6, 2), 2500m, 5)
                }));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetRevenue("monthly", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<RevenueReportResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Period.Should().Be("monthly");
        response.Data.TotalRevenue.Should().Be(5000m);
        response.Data.TotalReservations.Should().Be(10);
        response.Data.DailyBreakdown.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRevenue_WithInvalidPeriod_ReturnsEmptyNotThrow()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetRevenueReportAsync("bogus", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueReportResponse("bogus", 0m, 0, 0m, Array.Empty<RevenueReportBreakdownItemResponse>()));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetRevenue("bogus", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<RevenueReportResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.TotalRevenue.Should().Be(0m);
        response.Data.TotalReservations.Should().Be(0);
        response.Data.DailyBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOccupancy_WithValidPeriod_ReturnsOk()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetOccupancyReportAsync("weekly", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OccupancyReportResponse(
                "weekly",
                20,
                12,
                0.6m,
                new List<OccupancyReportBreakdownItemResponse>
                {
                    new(new DateOnly(2030, 6, 1), 12, 20, 0.6m)
                }));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetOccupancy("weekly", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<OccupancyReportResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.TotalVehicles.Should().Be(20);
        response.Data.OccupancyRate.Should().Be(0.6m);
    }

    [Fact]
    public async Task GetOccupancy_WithInvalidPeriod_ReturnsEmptyNotThrow()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetOccupancyReportAsync("bogus", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OccupancyReportResponse("bogus", 0, 0, 0m, Array.Empty<OccupancyReportBreakdownItemResponse>()));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetOccupancy("bogus", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<OccupancyReportResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.TotalVehicles.Should().Be(0);
        response.Data.DailyBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPopularVehicles_WithValidPeriod_ReturnsOk()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetPopularVehiclesAsync("monthly", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PopularVehicleReportItemResponse>
            {
                new("Renault Clio", 15, 7500m),
                new("Ford Focus", 10, 6000m)
            });

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetPopularVehicles("monthly", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<IReadOnlyList<PopularVehicleReportItemResponse>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Should().HaveCount(2);
        response.Data[0].VehicleName.Should().Be("Renault Clio");
        response.Data[0].RentalCount.Should().Be(15);
    }

    [Fact]
    public async Task GetPopularVehicles_WithInvalidPeriod_ReturnsEmptyNotThrow()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetPopularVehiclesAsync("bogus", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PopularVehicleReportItemResponse>());

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetPopularVehicles("bogus", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<IReadOnlyList<PopularVehicleReportItemResponse>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRevenue_InvokesServiceOnce()
    {
        var serviceMock = new Mock<IReportsService>();
        serviceMock.Setup(s => s.GetRevenueReportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueReportResponse("daily", 0m, 0, 0m, Array.Empty<RevenueReportBreakdownItemResponse>()));

        var controller = CreateController(serviceMock.Object);

        await controller.GetRevenue("daily", CancellationToken.None);

        serviceMock.Verify(s => s.GetRevenueReportAsync("daily", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AdminReportsController CreateController(IReportsService service)
    {
        return new AdminReportsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
