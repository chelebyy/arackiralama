using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ReservationQuoteServiceTests
{
    private readonly Mock<IPricingService> _pricingService = new();
    private readonly Mock<IReservationExtraPricingService> _extraPricingService = new();
    private readonly Mock<IReservationQuoteStore> _quoteStore = new();

    [Fact]
    public async Task CreateAsync_AddsGenericExtrasToFinalTotalAndStoresSessionHash()
    {
        var request = ValidRequest();
        SetupValidReferences(request);
        _pricingService
            .Setup(service => service.CalculateBreakdownAsync(
                request.VehicleGroupId,
                request.PickupOfficeId,
                request.ReturnOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                "SAVE10",
                0,
                0,
                request.DriverAge,
                request.FullCoverageWaiver,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BaseBreakdown());
        _extraPricingService
            .Setup(service => service.CalculateAsync(
                request.VehicleGroupId,
                request.Locale,
                3,
                request.SelectedExtras,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([QuotedExtra(total: 48m)]);
        ReservationQuoteV1? stored = null;
        _quoteStore
            .Setup(store => store.SaveAsync(It.IsAny<ReservationQuoteV1>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationQuoteV1, CancellationToken>((quote, _) => stored = quote)
            .Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(request, "public-session-123");

        result.ExtrasTotal.Should().Be(48m);
        result.FinalTotal.Should().Be(1498m);
        result.ExtraItems.Should().ContainSingle(item => item.Code == "gps" && item.Total == 48m);
        stored.Should().NotBeNull();
        stored!.SessionHash.Should().Be(ReservationQuoteSecurity.HashSessionId("public-session-123"));
        stored.SessionHash.Should().NotContain("public-session-123");
        stored.PricingSnapshot.FinalTotal.Should().Be(1498m);
        stored.PricingSnapshot.ExtrasTotal.Should().Be(48m);
        stored.ExpiresAtUtc.Should().BeCloseTo(stored.IssuedAtUtc.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_PreservesBuiltInFeesInExtrasTotal()
    {
        var request = ValidRequest();
        SetupValidReferences(request);
        _pricingService
            .Setup(service => service.CalculateBreakdownAsync(
                request.VehicleGroupId,
                request.PickupOfficeId,
                request.ReturnOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                "SAVE10",
                0,
                0,
                request.DriverAge,
                request.FullCoverageWaiver,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BaseBreakdown() with { ExtrasTotal = 75m, AirportFee = 75m, FinalTotal = 1525m });
        _extraPricingService
            .Setup(service => service.CalculateAsync(
                request.VehicleGroupId,
                request.Locale,
                3,
                request.SelectedExtras,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([QuotedExtra(48m)]);
        ReservationQuoteV1? stored = null;
        _quoteStore
            .Setup(store => store.SaveAsync(It.IsAny<ReservationQuoteV1>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationQuoteV1, CancellationToken>((quote, _) => stored = quote)
            .Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(request, "public-session-123");

        result.ExtrasTotal.Should().Be(123m);
        stored!.PricingSnapshot.ExtrasTotal.Should().Be(123m);
    }

    [Fact]
    public async Task CreateAsync_AppliesPercentageCampaignToGenericExtras()
    {
        var request = ValidRequest();
        SetupValidReferences(request);
        _pricingService
            .Setup(service => service.CalculateBreakdownAsync(
                request.VehicleGroupId,
                request.PickupOfficeId,
                request.ReturnOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                "SAVE10",
                0,
                0,
                request.DriverAge,
                request.FullCoverageWaiver,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BaseBreakdown() with
            {
                CampaignDiscount = 150m,
                FinalTotal = 1350m,
                AppliedCampaignDiscountType = "percentage",
                AppliedCampaignDiscountValue = 10m
            });
        _extraPricingService
            .Setup(service => service.CalculateAsync(
                request.VehicleGroupId,
                request.Locale,
                3,
                request.SelectedExtras,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([QuotedExtra(total: 50m)]);
        ReservationQuoteV1? stored = null;
        _quoteStore
            .Setup(store => store.SaveAsync(It.IsAny<ReservationQuoteV1>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationQuoteV1, CancellationToken>((quote, _) => stored = quote)
            .Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(request, "public-session-123");

        result.CampaignDiscount.Should().Be(155m);
        result.FinalTotal.Should().Be(1395m);
        stored.Should().NotBeNull();
        stored!.PricingSnapshot.DiscountTotal.Should().Be(155m);
        stored.PricingSnapshot.FinalTotal.Should().Be(1395m);
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingSessionBeforeCallingPricing()
    {
        var act = () => CreateService().CreateAsync(ValidRequest(), " ");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*X-Session-Id*");
        _pricingService.VerifyNoOtherCalls();
        _quoteStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_TreatsExplicitlyNullSelectedExtrasAsEmpty()
    {
        var request = ValidRequest() with { SelectedExtras = null! };
        SetupValidReferences(request);
        _pricingService
            .Setup(service => service.CalculateBreakdownAsync(
                request.VehicleGroupId,
                request.PickupOfficeId,
                request.ReturnOfficeId,
                request.PickupDateTimeUtc,
                request.ReturnDateTimeUtc,
                "SAVE10",
                0,
                0,
                request.DriverAge,
                request.FullCoverageWaiver,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BaseBreakdown());
        _extraPricingService
            .Setup(service => service.CalculateAsync(
                request.VehicleGroupId,
                request.Locale,
                3,
                It.Is<IReadOnlyList<SelectedReservationExtraInput>>(items => items.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _quoteStore
            .Setup(store => store.SaveAsync(It.IsAny<ReservationQuoteV1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(request, "public-session-123");

        result.ExtraItems.Should().BeEmpty();
        result.ExtrasTotal.Should().Be(0m);
        _extraPricingService.VerifyAll();
    }

    [Fact]
    public async Task CreateAsync_DoesNotAcceptAnyClientPriceFields()
    {
        typeof(SelectedReservationExtraInput).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(["OptionId", "Quantity", "OptionVersion"]);
        typeof(CreateReservationQuoteRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Price", StringComparison.OrdinalIgnoreCase) ||
                                        name.Contains("Total", StringComparison.OrdinalIgnoreCase));
    }

    private ReservationQuoteService CreateService() => new(
        _pricingService.Object,
        _extraPricingService.Object,
        _quoteStore.Object,
        NullLogger<ReservationQuoteService>.Instance);

    private void SetupValidReferences(CreateReservationQuoteRequest request)
    {
        _pricingService.Setup(service => service.VehicleGroupExistsAsync(request.VehicleGroupId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _pricingService.Setup(service => service.OfficeExistsAsync(request.PickupOfficeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _pricingService.Setup(service => service.OfficeExistsAsync(request.ReturnOfficeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _pricingService.Setup(service => service.IsCampaignCodeValidAsync(
                "SAVE10",
                request.VehicleGroupId,
                3,
                DateOnly.FromDateTime(request.PickupDateTimeUtc),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static CreateReservationQuoteRequest ValidRequest() => new()
    {
        VehicleGroupId = Guid.NewGuid(),
        PickupOfficeId = Guid.NewGuid(),
        ReturnOfficeId = Guid.NewGuid(),
        PickupDateTimeUtc = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
        ReturnDateTimeUtc = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc),
        CampaignCode = " save10 ",
        DriverAge = 30,
        Locale = "tr",
        SelectedExtras =
        [
            new SelectedReservationExtraInput
            {
                OptionId = Guid.NewGuid(),
                OptionVersion = 4,
                Quantity = 2
            }
        ]
    };

    private static PriceBreakdownDto BaseBreakdown() => new(
        DailyRate: 500m,
        RentalDays: 3,
        BaseTotal: 1500m,
        ExtrasTotal: 0m,
        CampaignDiscount: 50m,
        AirportFee: 0m,
        OneWayFee: 0m,
        ExtraDriverFee: 0m,
        ChildSeatFee: 0m,
        YoungDriverFee: 0m,
        FullCoverageWaiverFee: 0m,
        FinalTotal: 1450m,
        DepositAmount: 1000m,
        PreAuthorizationAmount: 1000m,
        Currency: "TRY",
        AppliedCampaignCode: "SAVE10");

    private static ReservationQuotedExtraV1 QuotedExtra(decimal total) => new()
    {
        ExtraOptionId = Guid.NewGuid(),
        OptionVersion = 4,
        Code = "gps",
        Locale = "tr",
        Name = "GPS",
        Description = "GPS",
        UnitPrice = 8m,
        PricingMode = "PER_DAY",
        Quantity = 2,
        RentalDays = 3,
        Total = total
    };
}
