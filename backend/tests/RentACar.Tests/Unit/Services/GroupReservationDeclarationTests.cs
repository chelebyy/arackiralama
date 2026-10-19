using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using StackExchange.Redis;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class GroupReservationDeclarationTests
{
    [Theory]
    [InlineData("quoted-age", false)]
    [InlineData("quoted-age", true)]
    [InlineData("minimum-age", false)]
    [InlineData("minimum-age", true)]
    [InlineData("licence-years", false)]
    [InlineData("licence-years", true)]
    [InlineData("licence-validity", false)]
    [InlineData("licence-validity", true)]
    [InlineData("documents", false)]
    [InlineData("documents", true)]
    [InlineData("underage", false)]
    [InlineData("underage", true)]
    [InlineData("missing", false)]
    [InlineData("missing", true)]
    public async Task NewGroupQuote_RejectsIneligibleDeclarationAndReleasesClaim(string invalid, bool unpaid)
    {
        await using var fixture = await Fixture.CreateAsync(invalid == "minimum-age" ? 20 : 30);
        var declaration = invalid switch
        {
            "quoted-age" => ValidDeclaration(22),
            "minimum-age" => ValidDeclaration(20),
            "licence-years" => ValidDeclaration(30) with { LicenseYearsAtPickup = 1 },
            "licence-validity" => ValidDeclaration(30) with { LicenseValidThroughReturn = false },
            "documents" => ValidDeclaration(30) with { DocumentsAvailableAtPickup = false },
            "underage" => ValidDeclaration(17),
            _ => null
        };
        var request = fixture.Request with { Driver = new DriverInfoRequest { Declaration = declaration } };
        Func<Task> create = () => fixture.CreateReservationAsync(request, unpaid);

        if (invalid is "quoted-age" or "minimum-age" or "licence-years")
            await create.Should().ThrowAsync<ReservationQuoteConflictException>();
        else
            await create.Should().ThrowAsync<ArgumentException>();

        (await fixture.Context.Reservations.CountAsync()).Should().Be(0);
        fixture.QuoteStore.Verify(store => store.ReleaseClaimAsync(
            fixture.Quote.QuoteId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.QuoteStore.Verify(store => store.MarkConsumedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        if (invalid != "minimum-age")
        {
            var retried = await fixture.CreateReservationAsync(fixture.Request, unpaid);
            retried.Driver!.Declaration.Should().BeEquivalentTo(fixture.Request.Driver!.Declaration);
            (await fixture.Context.Reservations.CountAsync()).Should().Be(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewGroupQuote_AcceptsEligibilityBoundaryAndPreservesExistingReplay(bool unpaid)
    {
        await using var fixture = await Fixture.CreateAsync(21);
        var request = fixture.Request with
        {
            Driver = new DriverInfoRequest { Declaration = ValidDeclaration(21) with { LicenseYearsAtPickup = 2 } }
        };
        var created = await fixture.CreateReservationAsync(request, unpaid);
        var stored = await fixture.Context.Reservations.SingleAsync();
        stored.DriverDeclaration.Should().BeEquivalentTo(request.Driver!.Declaration);
        stored.PricingSnapshot!.BookingConditions.Should().BeNull();
        stored.QuoteReplayProof!.SchemaVersion.Should().Be(1);

        fixture.Group.MinAge = 25;
        await fixture.Context.SaveChangesAsync();
        fixture.QuoteStore.Setup(store => store.GetAsync(fixture.Quote.QuoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationQuoteV1?)null);
        var replayed = await fixture.CreateReservationAsync(request with { IdempotencyKey = "retry-group-key" }, unpaid);
        replayed.Id.Should().Be(created.Id);
        (await fixture.Context.Reservations.CountAsync()).Should().Be(1);
    }

    private static DriverDeclaration ValidDeclaration(int age) => new()
    {
        AgeAtPickup = age, LicenseYearsAtPickup = 8,
        LicenseValidThroughReturn = true, DocumentsAvailableAtPickup = true
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TestDbContextFactory _factory = new();
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public RentACarDbContext Context { get; }
        public Mock<IReservationQuoteStore> QuoteStore { get; } = new();
        public VehicleGroup Group { get; } = new()
        {
            NameTr = "Group", NameEn = "Group", DepositAmount = 1000m, MinAge = 21, MinLicenseYears = 2
        };
        public ReservationQuoteV1 Quote { get; private set; } = null!;
        public CreateReservationRequest Request { get; private set; } = null!;
        public ReservationService Service { get; private set; } = null!;

        private Fixture() => Context = _factory.CreateContext();

        public static async Task<Fixture> CreateAsync(int quotedAge)
        {
            var fixture = new Fixture();
            var office = new Office { Name = "Office", Code = "GROUP" };
            var vehicle = new Vehicle
            {
                Plate = "07 GROUP 07", Brand = "Test", Model = "Car", Status = VehicleStatus.Available,
                Group = fixture.Group, GroupId = fixture.Group.Id, Office = office, OfficeId = office.Id
            };
            fixture.Context.AddRange(office, fixture.Group, vehicle);
            await fixture.Context.SaveChangesAsync();
            var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
            var quoteId = Guid.NewGuid();
            fixture.Quote = new ReservationQuoteV1
            {
                QuoteId = quoteId, SessionHash = ReservationQuoteSecurity.HashSessionId("group-session"),
                VehicleGroupId = fixture.Group.Id, PickupOfficeId = office.Id, ReturnOfficeId = office.Id,
                PickupDateTimeUtc = pickup, ReturnDateTimeUtc = pickup.AddDays(3), DriverAge = quotedAge,
                IssuedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                PricingSnapshot = new ReservationPricingSnapshotV1
                {
                    QuoteId = quoteId, IssuedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                    DailyRate = 500, RentalDays = 3, BaseTotal = 1500, FinalTotal = 1500,
                    DepositAmount = 1000, PreAuthorizationAmount = 1000, Currency = "TRY"
                }
            };
            fixture.QuoteStore.Setup(store => store.GetAsync(quoteId, It.IsAny<CancellationToken>())).ReturnsAsync(fixture.Quote);
            fixture.QuoteStore.Setup(store => store.TryClaimAsync(quoteId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            fixture.QuoteStore.Setup(store => store.ReleaseClaimAsync(quoteId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            fixture.QuoteStore.Setup(store => store.MarkConsumedAsync(quoteId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var extraPricing = new Mock<IReservationExtraPricingService>();
            extraPricing.Setup(service => service.ValidateCurrentAvailabilityAsync(
                fixture.Group.Id, It.IsAny<IReadOnlyList<ReservationQuotedExtraV1>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var fleet = new Mock<IFleetService>();
            fleet.Setup(service => service.SearchAvailableVehicleGroupsAsync(
                office.Id, pickup, pickup.AddDays(3), fixture.Group.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new RentACar.API.Contracts.Fleet.AvailableVehicleGroupDto(
                    fixture.Group.Id, "Group", "Group", 1, 500m, "TRY", 1000m, 21, 2, [], null)]);
            var vehicles = new VehicleRepository(fixture.Context);
            fixture.Service = new ReservationService(
                new ReservationRepository(fixture.Context), new CustomerRepository(fixture.Context), vehicles, vehicles,
                new OfficeRepository(fixture.Context), Mock.Of<IReservationHoldService>(), fixture.Context, fleet.Object,
                Mock.Of<IPricingService>(), Mock.Of<IPaymentService>(), Mock.Of<INotificationQueueService>(), fixture._cache,
                new AvailabilityCacheInvalidationSignal(), Mock.Of<IConnectionMultiplexer>(), new ConfigurationBuilder().Build(),
                NullLogger<ReservationService>.Instance, fixture.QuoteStore.Object, extraPricing.Object);
            fixture.Request = new CreateReservationRequest
            {
                VehicleGroupId = fixture.Group.Id, PickupOfficeId = office.Id, ReturnOfficeId = office.Id,
                PickupDateTimeUtc = pickup, ReturnDateTimeUtc = pickup.AddDays(3), DriverAge = quotedAge,
                QuoteId = quoteId, SessionId = "group-session", IdempotencyKey = "group-create-key",
                Driver = new DriverInfoRequest { Declaration = ValidDeclaration(quotedAge) },
                Customer = new CustomerInfoRequest
                {
                    FirstName = "Synthetic", LastName = "Group", Email = "group@example.test", Phone = "+900000000000"
                }
            };
            return fixture;
        }

        public Task<ReservationDto> CreateReservationAsync(CreateReservationRequest request, bool unpaid) => unpaid
            ? Service.CreateUnpaidRequestAsync(request) : Service.CreateDraftReservationAsync(request);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _cache.Dispose();
            _factory.Dispose();
        }
    }
}
