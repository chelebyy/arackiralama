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

public sealed class DurableReservationHoldTests
{
    [Fact]
    public async Task CreateHold_WhenRedisSucceeds_PersistsExpiryForCleanupAfterRedisLoss()
    {
        await using var context = CreateContext();
        var reservation = new Reservation { Status = ReservationStatus.Hold, VehicleId = Guid.NewGuid() };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var created = await service.CreateHoldAsync(reservation.Id, reservation.VehicleId, "guest", TimeSpan.FromMinutes(15));

        created.Should().BeTrue();
        var hold = await context.ReservationHolds.SingleOrDefaultAsync(h => h.ReservationId == reservation.Id);
        hold.Should().NotBeNull("Redis TTL must not erase the only expiry record");
        hold!.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        hold.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();
        (await service.GetExpiredHoldsAsync()).Should().Contain(reservation.Id);
    }

    private static RentACarDbContext CreateContext() => new(new DbContextOptionsBuilder<RentACarDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static RedisReservationHoldService CreateService(RentACarDbContext context)
    {
        var database = new Mock<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        return new RedisReservationHoldService(redis.Object, context, NullLogger<RedisReservationHoldService>.Instance);
    }
}
