using FluentAssertions;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Services;
using Xunit;

namespace RentACar.ApiIntegrationTests.Redis;

[Collection(IntegrationTestCollection.Name)]
public sealed class ReservationQuoteStoreIntegrationTests(RedisFixture redisFixture)
{
    [Fact]
    public async Task ClaimLifecycle_EnforcesOwnershipReleaseAndConsumption()
    {
        if (!redisFixture.IsAvailable)
        {
            return;
        }

        var store = new RedisReservationQuoteStore(redisFixture.ConnectionMultiplexer);
        var quote = CreateQuote();
        var database = redisFixture.ConnectionMultiplexer.GetDatabase();

        try
        {
            await store.SaveAsync(quote);
            var loaded = await store.GetAsync(quote.QuoteId);
            loaded.Should().NotBeNull();
            loaded!.SessionHash.Should().Be("HASHED-SESSION");

            var firstClaim = store.TryClaimAsync(quote.QuoteId, "owner-a", TimeSpan.FromSeconds(10));
            var secondClaim = store.TryClaimAsync(quote.QuoteId, "owner-b", TimeSpan.FromSeconds(10));
            var results = await Task.WhenAll(firstClaim, secondClaim);
            results.Should().ContainSingle(value => value);

            var owner = results[0] ? "owner-a" : "owner-b";
            var other = results[0] ? "owner-b" : "owner-a";
            (await store.ReleaseClaimAsync(quote.QuoteId, other)).Should().BeFalse();
            (await store.ReleaseClaimAsync(quote.QuoteId, owner)).Should().BeTrue();
            (await store.TryClaimAsync(quote.QuoteId, other, TimeSpan.FromSeconds(10))).Should().BeTrue();

            var reservationId = Guid.NewGuid();
            (await store.MarkConsumedAsync(quote.QuoteId, other, reservationId)).Should().BeTrue();
            (await store.GetAsync(quote.QuoteId)).Should().NotBeNull();
            (await store.TryClaimAsync(quote.QuoteId, "owner-c", TimeSpan.FromSeconds(10))).Should().BeFalse();
        }
        finally
        {
            await database.KeyDeleteAsync(
            [
                $"reservation_quote:{quote.QuoteId:N}",
                $"reservation_quote_claim:{quote.QuoteId:N}",
                $"reservation_quote_consumed:{quote.QuoteId:N}"
            ]);
        }
    }

    [Fact]
    public async Task ClaimLifecycle_ExpiredClaimCanBeAcquiredByAnotherOwner()
    {
        if (!redisFixture.IsAvailable)
        {
            return;
        }

        var store = new RedisReservationQuoteStore(redisFixture.ConnectionMultiplexer);
        var quote = CreateQuote();
        var database = redisFixture.ConnectionMultiplexer.GetDatabase();

        try
        {
            await store.SaveAsync(quote);
            (await store.TryClaimAsync(quote.QuoteId, "owner-a", TimeSpan.FromMilliseconds(100))).Should().BeTrue();
            await Task.Delay(200);
            (await store.TryClaimAsync(quote.QuoteId, "owner-b", TimeSpan.FromSeconds(10))).Should().BeTrue();
        }
        finally
        {
            await database.KeyDeleteAsync(
            [
                $"reservation_quote:{quote.QuoteId:N}",
                $"reservation_quote_claim:{quote.QuoteId:N}",
                $"reservation_quote_consumed:{quote.QuoteId:N}"
            ]);
        }
    }

    private static ReservationQuoteV1 CreateQuote()
    {
        var now = DateTime.UtcNow;
        return new ReservationQuoteV1
        {
            QuoteId = Guid.NewGuid(),
            SessionHash = "HASHED-SESSION",
            VehicleGroupId = Guid.NewGuid(),
            PickupOfficeId = Guid.NewGuid(),
            ReturnOfficeId = Guid.NewGuid(),
            PickupDateTimeUtc = now.AddDays(1),
            ReturnDateTimeUtc = now.AddDays(3),
            Locale = "tr",
            PricingSnapshot = new ReservationPricingSnapshotV1
            {
                QuoteId = Guid.NewGuid(),
                IssuedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(15),
                Currency = "TRY"
            },
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(15)
        };
    }
}
