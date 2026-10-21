using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class RedisReservationHoldServiceTests : IDisposable
{
    private readonly RentACarDbContext _context = new(new DbContextOptionsBuilder<RentACarDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IConnectionMultiplexer> _redis = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly RedisReservationHoldService _service;

    public RedisReservationHoldServiceTests()
    {
        _redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_database.Object);
        _service = new RedisReservationHoldService(_redis.Object, _context, NullLogger<RedisReservationHoldService>.Instance);
    }

    [Fact]
    public async Task Expiry_AfterRedisLossAndServiceRestart_ReleasesStockBlockingStatus()
    {
        var reservation = await SeedAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(15));
        var hold = await _context.ReservationHolds.SingleAsync();
        hold.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _context.SaveChangesAsync();
        _redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Throws(new RedisTimeoutException("offline", CommandStatus.Unknown));
        var restarted = new RedisReservationHoldService(_redis.Object, _context, NullLogger<RedisReservationHoldService>.Instance);

        (await restarted.GetExpiredHoldsAsync()).Should().ContainSingle().Which.Should().Be(reservation.Id);
        (await restarted.ExpireHoldAsync(reservation.Id)).Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatus.Expired);
        (await _context.ReservationHolds.AnyAsync()).Should().BeFalse();
        (await restarted.IsVehicleHeldAsync(reservation.VehicleId)).Should().BeFalse();
        (await restarted.ExpireHoldAsync(reservation.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment)]
    [InlineData(ReservationStatus.Paid)]
    [InlineData(ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.Active)]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Expired)]
    public async Task Expiry_PreservesProgressedReservationStatus(ReservationStatus status)
        {
        var reservation = await SeedAsync(status);
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(1));
        var hold = await _context.ReservationHolds.SingleAsync();
        hold.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _context.SaveChangesAsync();

        await _service.ExpireHoldAsync(reservation.Id);

        reservation.Status.Should().Be(status);
        (await _context.ReservationHolds.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task StaleExpiry_AfterRecreation_DoesNotRemoveCurrentHold()
    {
        var reservation = await SeedAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "old", TimeSpan.FromMinutes(1));
        var old = await _context.ReservationHolds.SingleAsync();
        old.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _context.SaveChangesAsync();
        var ids = await _service.GetExpiredHoldsAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "new", TimeSpan.FromMinutes(15));
        var current = await _context.ReservationHolds.SingleAsync();
        current.Id.Should().NotBe(old.Id);

        await _service.ExpireHoldAsync(ids.Single());

        reservation.Status.Should().Be(ReservationStatus.Hold);
        (await _service.GetHoldAsync(reservation.Id))!.SessionId.Should().Be("new");
        (await _context.ReservationHolds.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Extension_UpdatesDurableExpiryAndStaleJobDoesNotExpireIt()
    {
        var reservation = await SeedAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(5));
        var original = (await _service.GetHoldAsync(reservation.Id))!;
        _redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Throws(new RedisTimeoutException("offline", CommandStatus.Unknown));

        (await _service.ExtendHoldAsync(reservation.Id, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))).Should().BeTrue();
        await _service.ExpireHoldAsync(reservation.Id);

        (await _service.GetHoldAsync(reservation.Id))!.ExpiresAt.Should().BeAfter(original.ExpiresAt);
        reservation.Status.Should().Be(ReservationStatus.Hold);
    }

    [Fact]
    public async Task ExpiredHold_CannotBeRevivedByExtension()
    {
        var reservation = await SeedAsync();
        _context.ReservationHolds.Add(new ReservationHold { ReservationId = reservation.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(-1) });
        await _context.SaveChangesAsync();

        (await _service.ExtendHoldAsync(reservation.Id, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15))).Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateExpiredRow_DoesNotExpireActiveHold()
    {
        var reservation = await SeedAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(5));
        _context.ReservationHolds.Add(new ReservationHold { ReservationId = reservation.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(-1) });
        await _context.SaveChangesAsync();

        (await _service.GetExpiredHoldsAsync()).Should().BeEmpty();
        await _service.ExpireHoldAsync(reservation.Id);
        reservation.Status.Should().Be(ReservationStatus.Hold);
        (await _service.IsHoldValidAsync(reservation.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Release_RemovesAllMetadataAndReturnsHoldToDraft()
    {
        var reservation = await SeedAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(5));
        _context.ReservationHolds.Add(new ReservationHold { ReservationId = reservation.Id, VehicleId = reservation.VehicleId });
        await _context.SaveChangesAsync();

        await _service.ReleaseHoldAsync(reservation.Id);

        reservation.Status.Should().Be(ReservationStatus.Draft);
        (await _context.ReservationHolds.AnyAsync()).Should().BeFalse();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(5));
        (await _service.IsHoldValidAsync(reservation.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Release_PreservesPendingCancellationChanges()
    {
        var reservation = await SeedAsync();
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(5));
        reservation.Status = ReservationStatus.Cancelled;

        await _service.ReleaseHoldAsync(reservation.Id);

        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        (await _context.ReservationHolds.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task RedisOnlyOrStaleKeys_DoNotAuthorizeHoldOrBlockVehicle()
    {
        _database.Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        var id = Guid.NewGuid();

        (await _service.IsHoldValidAsync(id)).Should().BeFalse();
        (await _service.GetHoldAsync(id)).Should().BeNull();
        (await _service.IsVehicleHeldAsync(id)).Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseHold_PreservesGuestSessionExclusionAndMaximumDuration()
    {
        var reservation = await SeedAsync();
        _redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Throws(new RedisTimeoutException("offline", CommandStatus.Unknown));
        await _service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromHours(1));

        (await _service.IsVehicleHeldAsync(reservation.VehicleId)).Should().BeTrue();
        (await _service.IsVehicleHeldAsync(reservation.VehicleId, "guest")).Should().BeFalse();
        (await _service.GetHoldAsync(reservation.Id))!.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(15));
        await _service.ExtendHoldAsync(reservation.Id, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15));
        (await _service.GetHoldAsync(reservation.Id))!.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(15));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public async Task Create_RejectsInvalidSessionBeforePersistence(int length)
    {
        var action = () => _service.CreateHoldAsync(Guid.NewGuid(), Guid.NewGuid(), new string('s', length), TimeSpan.FromMinutes(15));
        await action.Should().ThrowAsync<InvalidOperationException>();
        (await _context.ReservationHolds.AnyAsync()).Should().BeFalse();
    }

    private async Task<Reservation> SeedAsync(ReservationStatus status = ReservationStatus.Hold)
    {
        var reservation = new Reservation { Status = status, VehicleId = Guid.NewGuid() };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public void Dispose() => _context.Dispose();
    }
