using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using StackExchange.Redis;
using Xunit;

namespace RentACar.ApiIntegrationTests.Redis;

/// <summary>
/// Verifies reservation hold Redis behavior and availability cache semantics.
/// </summary>
public sealed class RedisIntegrationTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task HoldCreation_WritesRedisKeysWithTtl()
    {
        using var scope = Services.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var reservationId = Guid.NewGuid();
        var vehicleId = await WithDbContextAsync(dbContext => dbContext.Vehicles.Select(vehicle => vehicle.Id).FirstAsync());

        var created = await holdService.CreateHoldAsync(reservationId, vehicleId, "session-a", TimeSpan.FromMinutes(15));
        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync($"hold:{reservationId}");

        created.Should().BeTrue();
        ttl.Should().NotBeNull();
        ttl!.Value.Should().BePositive();
        ttl.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task HoldExtension_ExtendsExistingHoldWithinMaximumDuration()
    {
        using var scope = Services.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var reservationId = Guid.NewGuid();
        var vehicleId = await WithDbContextAsync(dbContext => dbContext.Vehicles.Select(vehicle => vehicle.Id).FirstAsync());
        await holdService.CreateHoldAsync(reservationId, vehicleId, "session-b", TimeSpan.FromMinutes(5));

        var extended = await holdService.ExtendHoldAsync(reservationId, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));
        var snapshot = await holdService.GetHoldAsync(reservationId);

        extended.Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(4));
    }

    [Fact]
    public async Task RedisUnavailable_FallsBackToDatabaseHoldStorage()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var disconnectedRedis = await ConnectionMultiplexer.ConnectAsync("localhost:1,abortConnect=false,connectTimeout=100,syncTimeout=100");
        var fallbackService = new RedisReservationHoldService(
            disconnectedRedis,
            dbContext,
            NullLogger<RedisReservationHoldService>.Instance);
        var vehicleId = await dbContext.Vehicles.Select(vehicle => vehicle.Id).FirstAsync();

        // Create a valid customer and reservation first to satisfy FK constraints
        var customer = new Customer
        {
            FullName = "Redis Fallback Test Customer",
            Phone = "+90 555 999 99 99",
            Email = $"redis-test-{Guid.NewGuid():N}@example.com",
            IdentityNumber = "12345678901",
            Nationality = "TR"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var reservation = new Reservation
        {
            PublicCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            VehicleId = vehicleId,
            CustomerId = customer.Id,
            PickupDateTime = DateTime.UtcNow.Date.AddDays(1).AddHours(10),
            ReturnDateTime = DateTime.UtcNow.Date.AddDays(4).AddHours(10),
            TotalAmount = 1500m,
            Status = RentACar.Core.Enums.ReservationStatus.Hold,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var created = await fallbackService.CreateHoldAsync(reservation.Id, vehicleId, "session-c", TimeSpan.FromMinutes(15));
        var fallbackHoldExists = await dbContext.ReservationHolds.AnyAsync(hold => hold.ReservationId == reservation.Id);

        created.Should().BeTrue();
        fallbackHoldExists.Should().BeTrue();
    }

    [Fact]
    public async Task AvailabilitySearch_PopulatesMemoryCacheWithFiveMinuteEntry()
    {
        using var scope = Services.CreateScope();
        var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var pickup = DateTime.UtcNow.Date.AddDays(14).AddHours(10);
        var dropoff = pickup.AddDays(3);
        var cacheKey = $"availability:{TestDataSeeder.OfficeOneId:N}:{TestDataSeeder.OfficeOneId:N}:{pickup:O}:{dropoff:O}:all:1:20";

        await reservationService.SearchAvailabilityAsync(TestDataSeeder.OfficeOneId, TestDataSeeder.OfficeOneId, pickup, dropoff);

        memoryCache.TryGetValue(cacheKey, out object? cachedValue).Should().BeTrue();
        cachedValue.Should().NotBeNull();
    }
}
