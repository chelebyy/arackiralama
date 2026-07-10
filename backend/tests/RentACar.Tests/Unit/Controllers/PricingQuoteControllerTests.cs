using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class PricingQuoteControllerTests
{
    [Fact]
    public async Task CreateQuote_ReturnsFlatQuoteAndNoStoreHeader()
    {
        var request = ValidRequest();
        var quote = new ReservationQuoteDto(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(15),
            500m,
            3,
            1500m,
            48m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            1548m,
            1000m,
            1000m,
            "TRY",
            null,
            []);
        var quoteService = new Mock<IReservationQuoteService>();
        quoteService.Setup(service => service.CreateAsync(request, "session-123", It.IsAny<CancellationToken>())).ReturnsAsync(quote);
        var controller = CreateController(quoteService.Object);

        var result = await controller.CreateQuote(request, "session-123", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        quote.GetType().GetProperty("PriceBreakdown").Should().BeNull();
        quote.FinalTotal.Should().Be(1548m);
    }

    [Fact]
    public async Task CreateQuote_InvalidInputReturnsBadRequestWithNoStoreHeader()
    {
        var request = ValidRequest();
        var quoteService = new Mock<IReservationQuoteService>();
        quoteService.Setup(service => service.CreateAsync(request, "session-123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid quote"));
        var controller = CreateController(quoteService.Object);

        var result = await controller.CreateQuote(request, "session-123", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    private static PricingController CreateController(IReservationQuoteService quoteService)
    {
        var controller = new PricingController(Mock.Of<IPricingService>(), quoteService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static CreateReservationQuoteRequest ValidRequest() => new()
    {
        VehicleGroupId = Guid.NewGuid(),
        PickupOfficeId = Guid.NewGuid(),
        ReturnOfficeId = Guid.NewGuid(),
        PickupDateTimeUtc = DateTime.UtcNow.AddDays(1),
        ReturnDateTimeUtc = DateTime.UtcNow.AddDays(4),
        Locale = "tr"
    };
}
