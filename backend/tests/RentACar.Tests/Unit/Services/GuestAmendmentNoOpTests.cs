using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class GuestAmendmentNoOpTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local, DateTimeKind.Local)]
    [InlineData(DateTimeKind.Local, DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Unspecified)]
    public async Task QuoteAmendmentAsync_RejectsEquivalentUtcItineraryBeforeQuotingOrPersistence(
        DateTimeKind pickupKind, DateTimeKind returnKind)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var fixture = await CreateFixtureAsync(db);
        var request = Request(Represent(fixture.Reservation.PickupDateTime, pickupKind),
            Represent(fixture.Reservation.ReturnDateTime, returnKind));

        var error = await Record.ExceptionAsync(() => fixture.Service.QuoteAmendmentAsync(
            fixture.Access, request, CancellationToken.None));

        using var assertions = new AssertionScope();
        error.Should().BeOfType<ArgumentException>();
        fixture.Pricing.Invocations.Count.Should().Be(0);
        fixture.QuoteStore.Invocations.Count.Should().Be(0);
        (await db.ReservationAmendments.CountAsync()).Should().Be(0);
        db.ChangeTracker.Entries<ReservationAmendment>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, DateTimeKind.Utc)]
    [InlineData(false, DateTimeKind.Utc)]
    [InlineData(true, DateTimeKind.Local)]
    [InlineData(false, DateTimeKind.Local)]
    [InlineData(true, DateTimeKind.Unspecified)]
    [InlineData(false, DateTimeKind.Unspecified)]
    public async Task QuoteAmendmentAsync_AllowsTimeOfDayChangeToEitherBoundary(
        bool changePickup, DateTimeKind kind)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var fixture = await CreateFixtureAsync(db);
        var pickup = fixture.Reservation.PickupDateTime.AddHours(changePickup ? 1 : 0);
        var returnTime = fixture.Reservation.ReturnDateTime.AddHours(changePickup ? 0 : 1);

        var result = await fixture.Service.QuoteAmendmentAsync(fixture.Access,
            Request(Represent(pickup, kind), Represent(returnTime, kind)), CancellationToken.None);

        result.Should().NotBeNull();
        fixture.QuoteStore.Verify(store => store.SaveAsync(
            It.IsAny<ReservationQuoteV1>(), It.IsAny<CancellationToken>()), Times.Once);
        var quote = (ReservationQuoteV1)fixture.QuoteStore.Invocations.Single().Arguments[0];
        quote.PickupDateTimeUtc.Should().Be(pickup);
        quote.ReturnDateTimeUtc.Should().Be(returnTime);
        quote.PickupDateTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        quote.ReturnDateTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        quote.AmendmentReservationId.Should().Be(fixture.Reservation.Id);
        var amendment = await db.ReservationAmendments.SingleAsync();
        amendment.QuoteId.Should().Be(quote.QuoteId);
        amendment.ReservationId.Should().Be(fixture.Reservation.Id);
        amendment.AccessId.Should().Be(fixture.Access.Id);
    }

    private static DateTime Represent(DateTime utc, DateTimeKind kind) => kind == DateTimeKind.Local
        ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, kind);

    private static GuestAmendmentRequest Request(DateTime pickup, DateTime returnTime) => new(
        pickup, returnTime, new DriverDeclaration
        {
            AgeAtPickup = 30, LicenseYearsAtPickup = 8,
            LicenseValidThroughReturn = true, DocumentsAvailableAtPickup = true
        });

    private static async Task<Fixture> CreateFixtureAsync(RentACarDbContext db)
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var windows = Enum.GetValues<DayOfWeek>().Select(day => new OfficeOperatingWindow
            { Day = day, StartMinute = 0, EndMinute = 1440 }).ToList();
        var office = new Office
        {
            Name = "Test Office", Code = "NOOP", OperatingPolicy = new OfficeOperatingPolicy
            {
                MinimumNoticeMinutes = 0, PreparationMinutes = 60,
                PickupWindows = windows, ReturnWindows = windows,
                GuestManagement = new GuestManagementPolicy
                    { AllowDateChange = true, ChangeNoticeMinutes = 0, ChangeFee = 100m }
            }
        };
        var group = new VehicleGroup { NameTr = "Test", NameEn = "Test", MinAge = 21, MinLicenseYears = 2 };
        var vehicle = new Vehicle
        {
            Plate = "07 NOOP 07", Brand = "Test", Model = "Car", Status = VehicleStatus.Available,
            Group = group, Office = office, RentalTerms = new VehicleRentalTerms
            {
                DepositAmount = 1000m, MinAge = 21, MinLicenseYears = 2,
                Rates = [new VehicleRentalRate { DailyPrice = 500m,
                    StartDate = DateOnly.FromDateTime(pickup).AddYears(-1),
                    EndDate = DateOnly.FromDateTime(pickup).AddYears(1) }]
            }
        };
        var reservation = new Reservation
        {
            PublicCode = "TEST-NOOP", Status = ReservationStatus.Confirmed, TotalAmount = 2000m,
            Customer = new Customer { FullName = "Test Guest", Email = "guest@example.test" },
            Vehicle = vehicle, PickupOffice = office, ReturnOffice = office,
            PickupDateTime = pickup, ReturnDateTime = pickup.AddDays(4),
            PricingSnapshot = new ReservationPricingSnapshotV1
                { Currency = "TRY", BookingConditions = new ReservationBookingConditions { PaymentAtPickup = true } }
        };
        var access = new GuestReservationAccess { ReservationId = reservation.Id, Locale = "en" };
        db.AddRange(reservation, access);
        await db.SaveChangesAsync();
        var pricing = new Mock<IPricingService>();
        pricing.Setup(service => service.OfficeExistsAsync(office.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var store = new Mock<IReservationQuoteStore>();
        store.Setup(value => value.SaveAsync(It.IsAny<ReservationQuoteV1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var extras = new ReservationExtraPricingService(db);
        var booking = new VehicleBookingService(db, new PricingService(db, Mock.Of<IUnitOfWork>()), extras);
        var quotes = new ReservationQuoteService(pricing.Object, extras, store.Object,
            NullLogger<ReservationQuoteService>.Instance, Mock.Of<IVehicleRepository>(),
            Mock.Of<IReservationRepository>(), booking);
        var service = new GuestReservationService(db, Mock.Of<INotificationQueueService>(),
            new EphemeralDataProtectionProvider(), quotes, store.Object, booking, new AvailabilityCacheInvalidationSignal());
        return new Fixture(service, access, reservation, pricing, store);
    }

    private sealed record Fixture(GuestReservationService Service, GuestReservationAccess Access,
        Reservation Reservation, Mock<IPricingService> Pricing, Mock<IReservationQuoteStore> QuoteStore);
}
