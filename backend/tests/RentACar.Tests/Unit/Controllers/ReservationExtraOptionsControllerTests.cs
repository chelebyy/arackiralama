using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.ReservationExtraOptions;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class ReservationExtraOptionsControllerTests
{
    [Fact]
    public void AdminController_RequiresAdminPolicyAndStandardRateLimit()
    {
        typeof(AdminReservationExtraOptionsController)
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(AuthPolicyNames.AdminOnly);
        typeof(AdminReservationExtraOptionsController)
            .GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(RateLimitPolicyNames.Standard);
    }

    [Fact]
    public void PublicController_AllowsAnonymousAndUsesStandardRateLimit()
    {
        typeof(ReservationExtraOptionsController).GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        typeof(ReservationExtraOptionsController)
            .GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(RateLimitPolicyNames.Standard);
    }

    [Fact]
    public async Task AdminUpdate_MapsMissingRecordToNotFound()
    {
        var service = new Mock<IReservationExtraOptionCatalogService>();
        service.Setup(item => item.UpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<UpdateReservationExtraOptionRequest>(),
                It.IsAny<ReservationExtraOptionAuditContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReservationExtraOptionNotFoundException());
        var controller = CreateAdminController(service.Object);

        var result = await controller.Update(
            Guid.NewGuid(),
            new UpdateReservationExtraOptionRequest(1, 1, Core.Enums.ReservationExtraPricingMode.PerDay, 1, "wifi", 1, [], []),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminUpdate_MapsStaleVersionToConflict()
    {
        var service = new Mock<IReservationExtraOptionCatalogService>();
        service.Setup(item => item.UpdateAsync(
                It.IsAny<Guid>(),
                It.IsAny<UpdateReservationExtraOptionRequest>(),
                It.IsAny<ReservationExtraOptionAuditContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReservationExtraOptionConcurrencyException());
        var controller = CreateAdminController(service.Object);

        var result = await controller.Update(
            Guid.NewGuid(),
            new UpdateReservationExtraOptionRequest(1, 1, Core.Enums.ReservationExtraPricingMode.PerDay, 1, "wifi", 1, [], []),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeOfType<ApiResponse<object>>();
    }

    [Fact]
    public async Task PublicGet_MapsInvalidInputToBadRequest()
    {
        var service = new Mock<IReservationExtraOptionCatalogService>();
        service.Setup(item => item.GetPublicCatalogAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Locale is not supported."));
        var controller = new ReservationExtraOptionsController(service.Object);

        var result = await controller.Get(Guid.NewGuid(), "xx", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AdminReservationExtraOptionsController CreateAdminController(
        IReservationExtraOptionCatalogService service) => new(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}
