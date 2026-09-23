using System.Text.Json;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;
using StackExchange.Redis;

namespace RentACar.Infrastructure.Services;

public sealed class RedisReservationQuoteStore(IConnectionMultiplexer redis) : IReservationQuoteStore
{
    private const string QuoteKeyPrefix = "reservation_quote:";
    private const string ClaimKeyPrefix = "reservation_quote_claim:";
    private const string ConsumedKeyPrefix = "reservation_quote_consumed:";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const string TryClaimScript = """
        if redis.call('EXISTS', KEYS[1]) == 1 then
            return 0
        end
        if redis.call('EXISTS', KEYS[2]) == 0 then
            return 0
        end
        if redis.call('SET', KEYS[3], ARGV[1], 'NX', 'PX', ARGV[2]) then
            return 1
        end
        return 0
        """;

    private const string ReleaseClaimScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private const string MarkConsumedScript = """
        if redis.call('GET', KEYS[1]) ~= ARGV[1] then
            return 0
        end
        redis.call('SET', KEYS[2], ARGV[2], 'EX', ARGV[3])
        redis.call('DEL', KEYS[1])
        return 1
        """;

    private const string ReconcileConsumedScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
            return 0
        end
        redis.call('SET', KEYS[2], ARGV[1], 'EX', ARGV[2])
        redis.call('DEL', KEYS[3])
        return 1
        """;

    public async Task SaveAsync(ReservationQuoteV1 quote, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ttl = quote.ExpiresAtUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Cannot store an expired reservation quote.");
        }

        var saved = await redis.GetDatabase().StringSetAsync(
            QuoteKey(quote.QuoteId),
            JsonSerializer.Serialize(quote, SerializerOptions),
            ttl,
            When.NotExists);

        if (!saved)
        {
            throw new InvalidOperationException("Reservation quote identifier already exists.");
        }
    }

    public async Task<ReservationQuoteV1?> GetAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await redis.GetDatabase().StringGetAsync(QuoteKey(quoteId));
        return value.HasValue
            ? JsonSerializer.Deserialize<ReservationQuoteV1>(value.ToString(), SerializerOptions)
            : null;
    }

    public async Task<bool> TryClaimAsync(
        Guid quoteId,
        string claimOwner,
        TimeSpan claimDuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            TryClaimScript,
            [ConsumedKey(quoteId), QuoteKey(quoteId), ClaimKey(quoteId)],
            [claimOwner, (long)Math.Max(1, claimDuration.TotalMilliseconds)]);
        return (long)result == 1;
    }

    public async Task<bool> ReleaseClaimAsync(
        Guid quoteId,
        string claimOwner,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            ReleaseClaimScript,
            [ClaimKey(quoteId)],
            [claimOwner]);
        return (long)result == 1;
    }

    public async Task<bool> MarkConsumedAsync(
        Guid quoteId,
        string claimOwner,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            MarkConsumedScript,
            [ClaimKey(quoteId), ConsumedKey(quoteId)],
            [claimOwner, reservationId.ToString("N"), 86400]);
        return (long)result == 1;
    }

    public async Task<bool> ReconcileConsumedAsync(
        Guid quoteId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            ReconcileConsumedScript,
            [QuoteKey(quoteId), ConsumedKey(quoteId), ClaimKey(quoteId)],
            [reservationId.ToString("N"), 86400]);
        return (long)result == 1;
    }

    private static RedisKey QuoteKey(Guid quoteId) => $"{QuoteKeyPrefix}{quoteId:N}";
    private static RedisKey ClaimKey(Guid quoteId) => $"{ClaimKeyPrefix}{quoteId:N}";
    private static RedisKey ConsumedKey(Guid quoteId) => $"{ConsumedKeyPrefix}{quoteId:N}";
}
