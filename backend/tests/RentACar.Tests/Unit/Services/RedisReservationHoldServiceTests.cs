using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Services;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class RedisReservationHoldServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    public RedisReservationHoldServiceTests()
    {
        _redisMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);
    }

    [Fact]
    public async Task GetExpiredHoldsAsync_ReturnsOnlyExpiredReservationIdsFromDatabase()
    {
        var expiredReservationId = Guid.NewGuid();
        var activeReservationId = Guid.NewGuid();
        var holds = new List<ReservationHold>
        {
            new()
            {
                ReservationId = expiredReservationId,
                VehicleId = Guid.NewGuid(),
                SessionId = "expired",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new()
            {
                ReservationId = activeReservationId,
                VehicleId = Guid.NewGuid(),
                SessionId = "active",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            }
        };

        _dbContextMock
            .Setup(x => x.ReservationHolds)
            .Returns(holds.BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.GetExpiredHoldsAsync(CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(expiredReservationId);
    }

    [Fact]
    public async Task IsHoldValidAsync_WhenRedisKeyExists_ReturnsTrue()
    {
        var reservationId = Guid.NewGuid();

        _databaseMock
            .Setup(x => x.KeyExistsAsync(It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        var result = await sut.IsHoldValidAsync(reservationId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsVehicleHeldAsync_WhenExcludedSessionOwnsHold_ReturnsFalse()
    {
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var sessionId = "session-123";
        var payload = JsonSerializer.Serialize(new
        {
            ReservationId = reservationId,
            VehicleId = vehicleId,
            SessionId = sessionId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });

        _databaseMock
            .Setup(x => x.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"vehicle_hold:{vehicleId}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)reservationId.ToString());
        _databaseMock
            .Setup(x => x.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)payload);

        var sut = CreateSut();

        var result = await sut.IsVehicleHeldAsync(vehicleId, sessionId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetHoldAsync_WhenRedisPayloadExists_ReturnsSnapshot()
    {
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(8);
        var payload = JsonSerializer.Serialize(new
        {
            ReservationId = reservationId,
            VehicleId = vehicleId,
            SessionId = "session-456",
            ExpiresAt = expiresAt
        });

        _databaseMock
            .Setup(x => x.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)payload);

        var sut = CreateSut();

        var result = await sut.GetHoldAsync(reservationId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReservationId.Should().Be(reservationId);
        result.VehicleId.Should().Be(vehicleId);
        result.SessionId.Should().Be("session-456");
        result.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GetHoldAsync_WhenRedisPayloadMissing_FallsBackToDatabaseHold()
    {
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(12);
        var holds = new List<ReservationHold>
        {
            new()
            {
                ReservationId = reservationId,
                VehicleId = vehicleId,
                SessionId = "db-session",
                ExpiresAt = expiresAt
            }
        };

        _databaseMock
            .Setup(x => x.StringGetAsync(It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        _dbContextMock
            .Setup(x => x.ReservationHolds)
            .Returns(holds.BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.GetHoldAsync(reservationId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReservationId.Should().Be(reservationId);
        result.VehicleId.Should().Be(vehicleId);
        result.SessionId.Should().Be("db-session");
        result.ExpiresAt.Should().Be(expiresAt);
    }

    private RedisReservationHoldService CreateSut() => new(
        _redisMock.Object,
        _dbContextMock.Object,
        NullLogger<RedisReservationHoldService>.Instance);
}
