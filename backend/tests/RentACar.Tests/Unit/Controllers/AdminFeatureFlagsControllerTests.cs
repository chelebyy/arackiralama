using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.FeatureFlags;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;
using Microsoft.AspNetCore.Http;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminFeatureFlagsControllerTests
{
    [Fact]
    public async Task GetAll_ReturnsFeatureFlags()
    {
        var serviceMock = new Mock<IFeatureFlagService>();
        serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlagDto>
            {
                new(Guid.NewGuid(), "EnableNewBooking", true, "Yeni rezervasyon akisi", DateTime.UtcNow)
            });

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().NotBeNull().And.BeAssignableTo<ApiResponse<IReadOnlyList<FeatureFlagDto>>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithValidName_ReturnsOk()
    {
        var serviceMock = new Mock<IFeatureFlagService>();
        serviceMock.Setup(s => s.UpdateAsync("FeatureA", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlagDto(Guid.NewGuid(), "FeatureA", true, "Desc", DateTime.UtcNow));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.Update("FeatureA", new UpdateFeatureFlagRequest(true), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<FeatureFlagDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Contain("güncellendi");
    }

    [Fact]
    public async Task Update_WithEmptyName_ReturnsBadRequest()
    {
        var controller = CreateController(Mock.Of<IFeatureFlagService>());

        var result = await controller.Update("   ", new UpdateFeatureFlagRequest(true), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_WhenFlagNotFound_ReturnsNotFound()
    {
        var serviceMock = new Mock<IFeatureFlagService>();
        serviceMock.Setup(s => s.UpdateAsync("Missing", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureFlagDto?)null);

        var controller = CreateController(serviceMock.Object);

        var result = await controller.Update("Missing", new UpdateFeatureFlagRequest(true), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static AdminFeatureFlagsController CreateController(IFeatureFlagService service)
    {
        return new AdminFeatureFlagsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
