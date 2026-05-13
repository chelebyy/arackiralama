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

    [Fact]
    public async Task CreateHoldAsync_WhenRedisUnavailable_CreatesDatabaseFallbackHold()
    {
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var holds = new List<ReservationHold>();
        var holdSetMock = CreateReservationHoldSetMock(holds);

        _databaseMock
            .Setup(x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToResolvePhysicalConnection, "redis unavailable"));

        _dbContextMock
            .Setup(x => x.ReservationHolds)
            .Returns(holdSetMock.Object);
        _dbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut();

        var result = await sut.CreateHoldAsync(
            reservationId,
            vehicleId,
            "fallback-session",
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        result.Should().BeTrue();
        holdSetMock.Verify(
            x => x.AddAsync(
                It.Is<ReservationHold>(hold =>
                    hold.ReservationId == reservationId &&
                    hold.VehicleId == vehicleId &&
                    hold.SessionId == "fallback-session" &&
                    hold.ExpiresAt > DateTime.UtcNow.AddMinutes(9)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtendHoldAsync_WhenRedisUnavailable_ClampsDatabaseFallbackHoldToMaxDuration()
    {
        var reservationId = Guid.NewGuid();
        var initialExpiry = DateTime.UtcNow.AddMinutes(5);
        var holds = new List<ReservationHold>
        {
            new()
            {
                ReservationId = reservationId,
                VehicleId = Guid.NewGuid(),
                SessionId = "fallback-session",
                ExpiresAt = initialExpiry
            }
        };
        var holdSetMock = CreateReservationHoldSetMock(holds);

        _databaseMock
            .Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToResolvePhysicalConnection, "redis unavailable"));

        _dbContextMock
            .Setup(x => x.ReservationHolds)
            .Returns(holdSetMock.Object);
        _dbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut();

        var result = await sut.ExtendHoldAsync(
            reservationId,
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        result.Should().BeTrue();
        holds.Should().ContainSingle();
        holds[0].ExpiresAt.Should().BeAfter(initialExpiry);
        holds[0].ExpiresAt.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(10).AddSeconds(1));
    }

    [Fact]
    public async Task ReleaseHoldAsync_WhenRedisUnavailable_RemovesDatabaseFallbackHold()
    {
        var reservationId = Guid.NewGuid();
        var hold = new ReservationHold
        {
            ReservationId = reservationId,
            VehicleId = Guid.NewGuid(),
            SessionId = "fallback-session",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        var holds = new List<ReservationHold> { hold };
        var holdSetMock = CreateReservationHoldSetMock(holds);

        _databaseMock
            .Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToResolvePhysicalConnection, "redis unavailable"));

        _dbContextMock
            .Setup(x => x.ReservationHolds)
            .Returns(holdSetMock.Object);
        _dbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut();

        var result = await sut.ReleaseHoldAsync(reservationId, CancellationToken.None);

        result.Should().BeTrue();
        holdSetMock.Verify(x => x.Remove(It.Is<ReservationHold>(existing => existing == hold)), Times.Once);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtendHoldAsync_WhenRedisHoldMissing_ReturnsFalse()
    {
        var reservationId = Guid.NewGuid();

        _databaseMock
            .Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var sut = CreateSut();

        var result = await sut.ExtendHoldAsync(
            reservationId,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtendHoldAsync_WhenRedisPayloadDeserializesToNull_ReturnsFalse()
    {
        var reservationId = Guid.NewGuid();

        _databaseMock
            .Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"null");

        var sut = CreateSut();

        var result = await sut.ExtendHoldAsync(
            reservationId,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseHoldAsync_WhenRedisPayloadExists_DeletesVehicleAndHoldKeys()
    {
        var reservationId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            ReservationId = reservationId,
            VehicleId = vehicleId,
            SessionId = "session-789",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });

        _databaseMock
            .Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)payload);
        _databaseMock
            .Setup(x => x.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _dbContextMock
            .Setup(x => x.ReservationHolds)
            .Returns(new List<ReservationHold>().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.ReleaseHoldAsync(reservationId, CancellationToken.None);

        result.Should().BeTrue();
        _databaseMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"vehicle_hold:{vehicleId}"),
                It.IsAny<CommandFlags>()),
            Times.Once);
        _databaseMock.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"hold:{reservationId}"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private static Mock<Microsoft.EntityFrameworkCore.DbSet<ReservationHold>> CreateReservationHoldSetMock(List<ReservationHold> holds)
    {
        var holdSetMock = holds.BuildMockDbSet();

        holdSetMock
            .Setup(x => x.AddAsync(It.IsAny<ReservationHold>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationHold, CancellationToken>((hold, _) => holds.Add(hold))
            .ReturnsAsync((ReservationHold hold, CancellationToken _) => null!);

        holdSetMock
            .Setup(x => x.Remove(It.IsAny<ReservationHold>()))
            .Callback<ReservationHold>(hold => holds.Remove(hold))
            .Returns((ReservationHold _) => null!);

        return holdSetMock;
    }

    private RedisReservationHoldService CreateSut() => new(
        _redisMock.Object,
        _dbContextMock.Object,
        NullLogger<RedisReservationHoldService>.Instance);
}
