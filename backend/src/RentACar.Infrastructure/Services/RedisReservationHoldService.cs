using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using StackExchange.Redis;

namespace RentACar.Infrastructure.Services;

public sealed class RedisReservationHoldService : IReservationHoldService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RedisReservationHoldService> _logger;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);
    private const string ReleaseVehicleScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    public RedisReservationHoldService(
        IConnectionMultiplexer redis,
        IApplicationDbContext dbContext,
        ILogger<RedisReservationHoldService> logger)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<bool> CreateHoldAsync(
        Guid reservationId,
        Guid vehicleId,
        string sessionId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > ReservationHold.SessionIdMaxLength)
            throw new InvalidOperationException("Session ID must contain between 1 and 120 characters.");
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        return WithReservationLockAsync(reservationId, async () =>
        {
            var previous = await LoadHoldsAsync(reservationId, cancellationToken);
            _dbContext.ReservationHolds.RemoveRange(previous);
            var hold = new ReservationHold
            {
                ReservationId = reservationId,
                VehicleId = vehicleId,
                SessionId = sessionId,
                ExpiresAt = DateTime.UtcNow.Add(duration > DefaultTtl ? DefaultTtl : duration)
            };
            await _dbContext.ReservationHolds.AddAsync(hold, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await ClearCacheAsync(reservationId, previous.Select(h => h.VehicleId));
            await CacheHoldAsync(hold);
            return true;
        }, cancellationToken);
    }

    public Task<bool> ExtendHoldAsync(
        Guid reservationId,
        TimeSpan extension,
        TimeSpan maxDuration,
        CancellationToken cancellationToken = default)
    {
        if (extension <= TimeSpan.Zero || maxDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(extension));

        return WithReservationLockAsync(reservationId, async () =>
        {
            var holds = await LoadHoldsAsync(reservationId, cancellationToken);
            var now = DateTime.UtcNow;
            var hold = holds.Where(h => h.ExpiresAt > now).MaxBy(h => h.ExpiresAt);
            if (hold is null) return false;

            var duration = hold.ExpiresAt - now + extension;
            hold.ExpiresAt = now.Add(duration > maxDuration ? maxDuration : duration);
            hold.UpdatedAt = now;
            _dbContext.ReservationHolds.RemoveRange(holds.Where(h => h.Id != hold.Id));
            await _dbContext.SaveChangesAsync(cancellationToken);
            await CacheHoldAsync(hold);
            return true;
        }, cancellationToken);
            }

    public Task<bool> ReleaseHoldAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        WithReservationLockAsync(reservationId, async () =>
            {
            var reservation = await LoadReservationAsync(reservationId, preserveChanges: true, cancellationToken);
            var holds = await LoadHoldsAsync(reservationId, cancellationToken);
            if (reservation?.Status == ReservationStatus.Hold)
            {
                reservation.Status = ReservationStatus.Draft;
                reservation.UpdatedAt = DateTime.UtcNow;
            }
            _dbContext.ReservationHolds.RemoveRange(holds);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await ClearCacheAsync(reservationId, holds.Select(h => h.VehicleId)
                .Concat(reservation is null ? [] : new[] { reservation.VehicleId }));
            return true;
        }, cancellationToken);

    public Task<bool> ExpireHoldAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        WithReservationLockAsync(reservationId, async () =>
        {
            var holds = await LoadHoldsAsync(reservationId, cancellationToken);
            var now = DateTime.UtcNow;
            if (holds.Count == 0 || holds.Any(h => h.ExpiresAt > now)) return true;

            var reservation = await LoadReservationAsync(reservationId, preserveChanges: false, cancellationToken);
            if (reservation?.Status is ReservationStatus.Hold or ReservationStatus.Draft)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.UpdatedAt = now;
            }
            _dbContext.ReservationHolds.RemoveRange(holds);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await ClearCacheAsync(reservationId, holds.Select(h => h.VehicleId));
            return true;
        }, cancellationToken);

    public Task<bool> IsHoldValidAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        _dbContext.ReservationHolds.AsNoTracking()
            .AnyAsync(h => h.ReservationId == reservationId && h.ExpiresAt > DateTime.UtcNow, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetExpiredHoldsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbContext.ReservationHolds.AsNoTracking()
            .Where(h => h.ExpiresAt <= now && !_dbContext.ReservationHolds
                .Any(active => active.ReservationId == h.ReservationId && active.ExpiresAt > now))
            .Select(h => h.ReservationId).Distinct().ToListAsync(cancellationToken);
    }

    public Task<bool> IsVehicleHeldAsync(
        Guid vehicleId,
        string? excludeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ReservationHolds.AsNoTracking()
            .Where(h => h.VehicleId == vehicleId && h.ExpiresAt > DateTime.UtcNow);
            if (!string.IsNullOrEmpty(excludeSessionId))
            query = query.Where(h => h.SessionId != excludeSessionId);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<ReservationHoldSnapshot?> GetHoldAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var hold = await _dbContext.ReservationHolds.AsNoTracking()
            .Where(h => h.ReservationId == reservationId && h.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(h => h.ExpiresAt).FirstOrDefaultAsync(cancellationToken);
        return hold is null ? null : new ReservationHoldSnapshot(
            hold.ReservationId, hold.VehicleId, hold.SessionId, hold.ExpiresAt);
    }

    private async Task<T> WithReservationLockAsync<T>(
        Guid reservationId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var context = _dbContext as DbContext;
        var relational = context?.Database.IsRelational() == true;
        await using var transaction = relational && context!.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (relational)
            await context!.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM reservations WHERE id = {reservationId} FOR UPDATE", cancellationToken);

        var result = await action();
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<List<ReservationHold>> LoadHoldsAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries<ReservationHold>()
            .Where(e => e.Entity.ReservationId == reservationId).ToList())
        {
            if (entry.State != EntityState.Unchanged)
                throw new InvalidOperationException("Cannot mutate a hold with unsaved changes.");
            entry.State = EntityState.Detached;
        }
        return await _dbContext.ReservationHolds.Where(h => h.ReservationId == reservationId)
            .ToListAsync(cancellationToken);
    }

    private async Task<Reservation?> LoadReservationAsync(
        Guid reservationId, bool preserveChanges, CancellationToken cancellationToken)
    {
        var reservation = await _dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation is not null && _dbContext is DbContext context && context.Database.IsRelational())
        {
            var entry = context.Entry(reservation);
            if (entry.State == EntityState.Unchanged)
                await entry.ReloadAsync(cancellationToken);
            else if (!preserveChanges)
                throw new InvalidOperationException("Cannot expire a reservation with unsaved changes.");
        }
        return reservation;
    }

    private async Task CacheHoldAsync(ReservationHold hold)
    {
        try
        {
            var ttl = hold.ExpiresAt - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero) return;
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(new ReservationHoldSnapshot(
                hold.ReservationId, hold.VehicleId, hold.SessionId, hold.ExpiresAt));
            await db.StringSetAsync($"hold:{hold.ReservationId}", payload, ttl);
            await db.StringSetAsync($"vehicle_hold:{hold.VehicleId}", hold.ReservationId.ToString(), ttl);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis hold cache update failed; durable hold remains authoritative");
        }
    }

    private async Task ClearCacheAsync(Guid reservationId, IEnumerable<Guid> vehicleIds)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"hold:{reservationId}");
            foreach (var vehicleId in vehicleIds.Distinct())
                await db.ScriptEvaluateAsync(ReleaseVehicleScript,
                    [(RedisKey)$"vehicle_hold:{vehicleId}"], [(RedisValue)reservationId.ToString()]);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
            _logger.LogWarning(ex, "Redis hold cache cleanup failed; durable hold remains authoritative");
        }
    }
        }
