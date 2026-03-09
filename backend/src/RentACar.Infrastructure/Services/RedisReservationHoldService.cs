using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;
using StackExchange.Redis;

namespace RentACar.Infrastructure.Services;

public sealed class RedisReservationHoldService : IReservationHoldService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RedisReservationHoldService> _logger;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(15);
    private readonly string _keyPrefix = "hold:";
    private readonly string _vehiclePrefix = "vehicle_hold:";

    public RedisReservationHoldService(
        IConnectionMultiplexer redis,
        IApplicationDbContext dbContext,
        ILogger<RedisReservationHoldService> logger)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> CreateHoldAsync(
        Guid reservationId,
        Guid vehicleId,
        string sessionId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var holdKey = $"{_keyPrefix}{reservationId}";
            var vehicleKey = $"{_vehiclePrefix}{vehicleId}";

            var holdData = new ReservationHoldData
            {
                ReservationId = reservationId,
                VehicleId = vehicleId,
                SessionId = sessionId,
                ExpiresAt = DateTime.UtcNow.Add(duration)
            };

            var json = JsonSerializer.Serialize(holdData);
            var ttl = duration > _defaultTtl ? _defaultTtl : duration;

            // Store hold data
            await db.StringSetAsync(holdKey, json, ttl);
            
            // Track vehicle hold for quick lookup
            await db.StringSetAsync(vehicleKey, reservationId.ToString(), ttl);

            _logger.LogInformation(
                "Created Redis hold for reservation {ReservationId}, vehicle {VehicleId}, TTL {TtlMinutes}m",
                reservationId, vehicleId, ttl.TotalMinutes);

            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for hold creation");
            return await CreateDatabaseHoldAsync(reservationId, vehicleId, sessionId, duration, cancellationToken);
        }
    }

    public async Task<bool> ExtendHoldAsync(
        Guid reservationId,
        TimeSpan extension,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var holdKey = $"{_keyPrefix}{reservationId}";

            var existingData = await db.StringGetAsync(holdKey);
            if (!existingData.HasValue)
            {
                _logger.LogWarning("Cannot extend hold for {ReservationId}: not found in Redis", reservationId);
                return false;
            }

            var holdData = JsonSerializer.Deserialize<ReservationHoldData>(existingData.ToString());
            if (holdData == null)
            {
                return false;
            }

            var currentDuration = holdData.ExpiresAt - DateTime.UtcNow;
            var newDuration = currentDuration + extension;

            if (newDuration > maxDuration)
            {
                newDuration = maxDuration;
            }

            holdData.ExpiresAt = DateTime.UtcNow.Add(newDuration);
            var json = JsonSerializer.Serialize(holdData);

            await db.StringSetAsync(holdKey, json, newDuration);
            
            // Update vehicle hold TTL
            var vehicleKey = $"{_vehiclePrefix}{holdData.VehicleId}";
            await db.KeyExpireAsync(vehicleKey, newDuration);

            _logger.LogInformation(
                "Extended hold for reservation {ReservationId}, new TTL {TtlMinutes}m",
                reservationId, newDuration.TotalMinutes);

            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for hold extension");
            return await ExtendDatabaseHoldAsync(reservationId, extension, maxDuration, cancellationToken);
        }
    }

    public async Task<bool> ReleaseHoldAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var holdKey = $"{_keyPrefix}{reservationId}";

            var existingData = await db.StringGetAsync(holdKey);
            if (existingData.HasValue)
            {
                var holdData = JsonSerializer.Deserialize<ReservationHoldData>(existingData.ToString());
                if (holdData != null)
                {
                    var vehicleKey = $"{_vehiclePrefix}{holdData.VehicleId}";
                    await db.KeyDeleteAsync(vehicleKey);
                }
            }

            await db.KeyDeleteAsync(holdKey);
            
            // Also clean up database hold if exists
            await ReleaseDatabaseHoldAsync(reservationId, cancellationToken);

            _logger.LogInformation("Released hold for reservation {ReservationId}", reservationId);
            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for hold release");
            return await ReleaseDatabaseHoldAsync(reservationId, cancellationToken);
        }
    }

    public async Task<bool> IsHoldValidAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var holdKey = $"{_keyPrefix}{reservationId}";

            var exists = await db.KeyExistsAsync(holdKey);
            return exists;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for hold validation");
            return await IsDatabaseHoldValidAsync(reservationId, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetExpiredHoldsAsync(CancellationToken cancellationToken = default)
    {
        // Redis handles expiration automatically via TTL
        // This method is primarily for database fallback cleanup
        var expiredHolds = await _dbContext.ReservationHolds
            .AsNoTracking()
            .Where(h => h.ExpiresAt < DateTime.UtcNow)
            .Select(h => h.ReservationId)
            .ToListAsync(cancellationToken);

        return expiredHolds;
    }

    public async Task<bool> IsVehicleHeldAsync(
        Guid vehicleId,
        string? excludeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var vehicleKey = $"{_vehiclePrefix}{vehicleId}";

            var holdReservationId = await db.StringGetAsync(vehicleKey);
            if (!holdReservationId.HasValue)
            {
                return false;
            }

            // Check if session exclusion applies
            if (!string.IsNullOrEmpty(excludeSessionId))
            {
                var holdKey = $"{_keyPrefix}{holdReservationId}";
                var holdData = await db.StringGetAsync(holdKey);
                if (holdData.HasValue)
                {
                    var hold = JsonSerializer.Deserialize<ReservationHoldData>(holdData.ToString());
                    if (hold?.SessionId == excludeSessionId)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for vehicle hold check");
            return await IsVehicleHeldInDatabaseAsync(vehicleId, excludeSessionId, cancellationToken);
        }
    }

    public async Task<ReservationHoldSnapshot?> GetHoldAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var holdKey = $"{_keyPrefix}{reservationId}";
            var holdData = await db.StringGetAsync(holdKey);

            if (!holdData.HasValue)
            {
                return await GetDatabaseHoldAsync(reservationId, cancellationToken);
            }

            var payload = JsonSerializer.Deserialize<ReservationHoldData>(holdData.ToString());
            if (payload == null)
            {
                return await GetDatabaseHoldAsync(reservationId, cancellationToken);
            }

            return new ReservationHoldSnapshot(
                payload.ReservationId,
                payload.VehicleId,
                payload.SessionId,
                payload.ExpiresAt);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to database for hold retrieval");
            return await GetDatabaseHoldAsync(reservationId, cancellationToken);
        }
    }

    #region Database Fallback Methods

    private async Task<bool> CreateDatabaseHoldAsync(
        Guid reservationId,
        Guid vehicleId,
        string sessionId,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            var hold = new ReservationHold
            {
                ReservationId = reservationId,
                VehicleId = vehicleId,
                SessionId = sessionId,
                ExpiresAt = DateTime.UtcNow.Add(duration)
            };

            await _dbContext.ReservationHolds.AddAsync(hold, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created database fallback hold for reservation {ReservationId}",
                reservationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database fallback hold");
            return false;
        }
    }

    private async Task<bool> ExtendDatabaseHoldAsync(
        Guid reservationId,
        TimeSpan extension,
        TimeSpan maxDuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var hold = await _dbContext.ReservationHolds
                .FirstOrDefaultAsync(h => h.ReservationId == reservationId, cancellationToken);

            if (hold == null)
            {
                return false;
            }

            var currentDuration = hold.ExpiresAt - DateTime.UtcNow;
            var newDuration = currentDuration + extension;

            if (newDuration > maxDuration)
            {
                newDuration = maxDuration;
            }

            hold.ExpiresAt = DateTime.UtcNow.Add(newDuration);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend database fallback hold");
            return false;
        }
    }

    private async Task<bool> ReleaseDatabaseHoldAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        try
        {
            var hold = await _dbContext.ReservationHolds
                .FirstOrDefaultAsync(h => h.ReservationId == reservationId, cancellationToken);

            if (hold != null)
            {
                _dbContext.ReservationHolds.Remove(hold);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release database fallback hold");
            return false;
        }
    }

    private async Task<bool> IsDatabaseHoldValidAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var hold = await _dbContext.ReservationHolds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.ReservationId == reservationId, cancellationToken);

        return hold != null && hold.ExpiresAt > DateTime.UtcNow;
    }

    private async Task<bool> IsVehicleHeldInDatabaseAsync(
        Guid vehicleId,
        string? excludeSessionId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ReservationHolds
            .AsNoTracking()
            .Where(h => h.VehicleId == vehicleId && h.ExpiresAt > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(excludeSessionId))
        {
            query = query.Where(h => h.SessionId != excludeSessionId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<ReservationHoldSnapshot?> GetDatabaseHoldAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var hold = await _dbContext.ReservationHolds
            .AsNoTracking()
            .FirstOrDefaultAsync(
                h => h.ReservationId == reservationId && h.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (hold == null)
        {
            return null;
        }

        return new ReservationHoldSnapshot(
            hold.ReservationId,
            hold.VehicleId,
            hold.SessionId,
            hold.ExpiresAt);
    }

    #endregion

    private sealed class ReservationHoldData
    {
        public Guid ReservationId { get; set; }
        public Guid VehicleId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
