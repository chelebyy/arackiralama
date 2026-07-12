using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using StackExchange.Redis;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ReservationServiceQuotePersistenceTests
{
    [Fact]
    public async Task CreateDraftReservationAsync_PersistsQuoteSnapshotAndSelectedExtrasAtomicallyAndReplaysExisting()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var office = new Office { Name = "Office", Code = "OFF" };
        var group = new VehicleGroup { NameTr = "Group", NameEn = "Group", DepositAmount = 1000m };
        var vehicle = new Vehicle
        {
            Plate = "07 TEST 07",
            Brand = "Test",
            Model = "Car",
            Group = group,
            GroupId = group.Id,
            Office = office,
            OfficeId = office.Id,
            Status = VehicleStatus.Available
        };
        context.AddRange(office, group, vehicle);
        await context.SaveChangesAsync();

        var pickup = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var quote = CreateQuote(group.Id, office.Id, pickup);
        var quoteStore = new Mock<IReservationQuoteStore>();
        quoteStore.Setup(store => store.GetAsync(quote.QuoteId, It.IsAny<CancellationToken>())).ReturnsAsync(quote);
        quoteStore.Setup(store => store.TryClaimAsync(quote.QuoteId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        quoteStore.Setup(store => store.MarkConsumedAsync(quote.QuoteId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        quoteStore.Setup(store => store.ReconcileConsumedAsync(quote.QuoteId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var extraPricing = new Mock<IReservationExtraPricingService>();
        extraPricing.Setup(service => service.ValidateCurrentAvailabilityAsync(group.Id, quote.SelectedExtras, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var fleet = new Mock<IFleetService>();
        fleet.Setup(service => service.SearchAvailableVehicleGroupsAsync(
                office.Id,
                pickup,
                pickup.AddDays(3),
                group.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RentACar.API.Contracts.Fleet.AvailableVehicleGroupDto(group.Id, "Group", "Group", 1, 500m, "TRY", 1000m, 21, 2, [], null)]);

        var reservationRepository = new ReservationRepository(context);
        var customerRepository = new CustomerRepository(context);
        var vehicleRepository = new VehicleRepository(context);
        var officeRepository = new OfficeRepository(context);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new ReservationService(
            reservationRepository,
            customerRepository,
            vehicleRepository,
            vehicleRepository,
            officeRepository,
            Mock.Of<IReservationHoldService>(),
            context,
            fleet.Object,
            Mock.Of<IPricingService>(),
            Mock.Of<IPaymentService>(),
            Mock.Of<INotificationQueueService>(),
            memoryCache,
            new AvailabilityCacheInvalidationSignal(),
            Mock.Of<IConnectionMultiplexer>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ReservationService>.Instance,
            quoteStore.Object,
            extraPricing.Object);
        var request = new CreateReservationRequest
        {
            VehicleGroupId = group.Id,
            PickupOfficeId = office.Id,
            ReturnOfficeId = office.Id,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            QuoteId = quote.QuoteId,
            Locale = "tr",
            SessionId = "session-123",
            IdempotencyKey = "idempotency-123",
            Customer = new CustomerInfoRequest
            {
                FirstName = "Test",
                LastName = "Customer",
                Email = "quote-persistence@example.test",
                Phone = "+900000000000"
            }
        };

        var created = await service.CreateDraftReservationAsync(request);
        var replayed = await service.CreateDraftReservationAsync(request with { IdempotencyKey = "idempotency-456" });
        var crossSessionReplay = () => service.CreateDraftReservationAsync(request with
        {
            SessionId = "attacker-session",
            IdempotencyKey = "idempotency-789"
        });

        created.Id.Should().Be(replayed.Id);
        await crossSessionReplay.Should().ThrowAsync<ReservationQuoteConflictException>()
            .WithMessage("*session*");
        created.TotalAmount.Should().Be(1548m);
        created.BreakdownSource.Should().Be("SNAPSHOT");
        created.SelectedExtras.Should().ContainSingle(item => item.Code == "gps" && item.Total == 48m);
        created.PriceBreakdown!.BaseTotal.Should().Be(1500m);
        created.PriceBreakdown.ExtrasTotal.Should().Be(48m);
        created.PriceBreakdown.FinalTotal.Should().Be(1548m);

        var storedReservation = context.Reservations.Single();
        storedReservation.QuoteId.Should().Be(quote.QuoteId);
        storedReservation.PricingSnapshot!.FinalTotal.Should().Be(1548m);
        context.ReservationSelectedExtras.Should().ContainSingle(item =>
            item.ReservationId == storedReservation.Id &&
            item.OptionCodeSnapshot == "gps" &&
            item.TotalPriceSnapshot == 48m);
        quoteStore.Verify(store => store.TryClaimAsync(quote.QuoteId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        quoteStore.Verify(store => store.MarkConsumedAsync(quote.QuoteId, It.IsAny<string>(), storedReservation.Id, It.IsAny<CancellationToken>()), Times.Once);
        quoteStore.Verify(store => store.ReconcileConsumedAsync(quote.QuoteId, storedReservation.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraftReservationAsync_RejectsMixedQuoteAndLegacyExtras()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new ReservationService(
            new ReservationRepository(context),
            new CustomerRepository(context),
            new VehicleRepository(context),
            new VehicleRepository(context),
            new OfficeRepository(context),
            Mock.Of<IReservationHoldService>(),
            context,
            Mock.Of<IFleetService>(),
            Mock.Of<IPricingService>(),
            Mock.Of<IPaymentService>(),
            Mock.Of<INotificationQueueService>(),
            memoryCache,
            new AvailabilityCacheInvalidationSignal(),
            Mock.Of<IConnectionMultiplexer>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ReservationService>.Instance);

        var act = () => service.CreateDraftReservationAsync(new CreateReservationRequest
        {
            QuoteId = Guid.NewGuid(),
            ExtraDriverCount = 1
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be combined*");
        context.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDraftReservationAsync_AppliesPercentageCampaignToAdaptedLegacyExtras()
    {
        using var factory = new TestDbContextFactory();
        await using var context = factory.CreateContext();
        var office = new Office { Name = "Office", Code = "LEG" };
        var group = new VehicleGroup { NameTr = "Group", NameEn = "Group", DepositAmount = 1000m };
        var vehicle = new Vehicle
        {
            Plate = "07 LEG 07",
            Brand = "Test",
            Model = "Car",
            Group = group,
            GroupId = group.Id,
            Office = office,
            OfficeId = office.Id,
            Status = VehicleStatus.Available
        };
        context.AddRange(office, group, vehicle);
        await context.SaveChangesAsync();

        var pickup = new DateTime(2027, 8, 10, 10, 0, 0, DateTimeKind.Utc);
        var pricing = new Mock<IPricingService>();
        pricing.Setup(service => service.CalculateBreakdownAsync(
                group.Id,
                office.Id,
                office.Id,
                pickup,
                pickup.AddDays(2),
                "SAVE10",
                0,
                0,
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceBreakdownDto(
                500m, 2, 1000m, 100m, 110m, 100m, 0m, 0m, 0m, 0m, 0m,
                990m, 1000m, 1000m, "TRY", "SAVE10")
            {
                AppliedCampaignDiscountType = "percentage",
                AppliedCampaignDiscountValue = 10m
            });
        var extraPricing = new Mock<IReservationExtraPricingService>();
        extraPricing.Setup(service => service.CalculateLegacyAsync(
                group.Id,
                "tr",
                2,
                1,
                0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ReservationQuotedExtraV1
                {
                    ExtraOptionId = Guid.NewGuid(),
                    OptionVersion = 1,
                    Code = "additional_driver",
                    Locale = "tr",
                    Name = "Ek Sürücü",
                    Description = "Ek sürücü",
                    UnitPrice = 200m,
                    PricingMode = "PER_RENTAL",
                    Quantity = 1,
                    RentalDays = 2,
                    Total = 200m
                }
            ]);
        var fleet = new Mock<IFleetService>();
        fleet.Setup(service => service.SearchAvailableVehicleGroupsAsync(
                office.Id,
                pickup,
                pickup.AddDays(2),
                group.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RentACar.API.Contracts.Fleet.AvailableVehicleGroupDto(
                group.Id, "Group", "Group", 1, 500m, "TRY", 1000m, 21, 2, [], null)]);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new ReservationService(
            new ReservationRepository(context),
            new CustomerRepository(context),
            new VehicleRepository(context),
            new VehicleRepository(context),
            new OfficeRepository(context),
            Mock.Of<IReservationHoldService>(),
            context,
            fleet.Object,
            pricing.Object,
            Mock.Of<IPaymentService>(),
            Mock.Of<INotificationQueueService>(),
            memoryCache,
            new AvailabilityCacheInvalidationSignal(),
            Mock.Of<IConnectionMultiplexer>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ReservationService>.Instance,
            null,
            extraPricing.Object);

        var result = await service.CreateDraftReservationAsync(new CreateReservationRequest
        {
            VehicleGroupId = group.Id,
            PickupOfficeId = office.Id,
            ReturnOfficeId = office.Id,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(2),
            CampaignCode = "SAVE10",
            ExtraDriverCount = 1,
            Locale = "tr",
            Customer = new CustomerInfoRequest
            {
                FirstName = "Legacy",
                LastName = "Campaign",
                Email = "legacy-campaign@example.test",
                Phone = "+905551234567"
            }
        });

        result.PriceBreakdown!.ExtrasTotal.Should().Be(300m);
        result.PriceBreakdown.AirportFee.Should().Be(100m);
        result.PriceBreakdown.CampaignDiscount.Should().Be(130m);
        result.PriceBreakdown.FinalTotal.Should().Be(1170m);
        result.TotalAmount.Should().Be(1170m);
    }

    private static ReservationQuoteV1 CreateQuote(Guid groupId, Guid officeId, DateTime pickup)
    {
        var quoteId = Guid.NewGuid();
        var extraId = Guid.NewGuid();
        var issuedAt = DateTime.UtcNow;
        var extra = new ReservationQuotedExtraV1
        {
            ExtraOptionId = extraId,
            OptionVersion = 5,
            Code = "gps",
            Locale = "tr",
            Name = "GPS",
            Description = "GPS cihazı",
            UnitPrice = 8m,
            PricingMode = "PER_DAY",
            Quantity = 2,
            RentalDays = 3,
            Total = 48m
        };
        return new ReservationQuoteV1
        {
            QuoteId = quoteId,
            SessionHash = ReservationQuoteSecurity.HashSessionId("session-123"),
            VehicleGroupId = groupId,
            PickupOfficeId = officeId,
            ReturnOfficeId = officeId,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            Locale = "tr",
            SelectedExtras = [extra],
            PricingSnapshot = new ReservationPricingSnapshotV1
            {
                QuoteId = quoteId,
                IssuedAtUtc = issuedAt,
                ExpiresAtUtc = issuedAt.AddMinutes(15),
                DailyRate = 500m,
                RentalDays = 3,
                BaseTotal = 1500m,
                ExtrasTotal = 48m,
                FinalTotal = 1548m,
                DepositAmount = 1000m,
                PreAuthorizationAmount = 1000m,
                Currency = "TRY",
                ExtraItems =
                [
                    new ReservationPricingExtraSnapshot
                    {
                        ExtraOptionId = extraId,
                        OptionVersion = 5,
                        Code = "gps",
                        Name = "GPS",
                        UnitPrice = 8m,
                        PricingMode = "PER_DAY",
                        Quantity = 2,
                        RentalDays = 3,
                        Total = 48m
                    }
                ]
            },
            IssuedAtUtc = issuedAt,
            ExpiresAtUtc = issuedAt.AddMinutes(15)
        };
    }
}
