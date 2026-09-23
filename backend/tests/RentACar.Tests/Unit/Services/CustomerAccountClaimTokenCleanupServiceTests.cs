using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Services;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class CustomerAccountClaimTokenCleanupServiceTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public CustomerAccountClaimTokenCleanupServiceTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task CleanupAsync_DeletesOnlyStaleTokensInBoundedBatches()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var utcNow = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var customer = new Customer
        {
            Email = "cleanup@test.com",
            FullName = "Cleanup Customer",
            Phone = string.Empty,
            IdentityNumber = string.Empty,
            Nationality = "TR",
            LicenseYear = 0
        };
        dbContext.Customers.Add(customer);

        var staleExpired = CreateToken(customer.Id, "stale-expired", utcNow.AddDays(-20));
        var staleConsumed = CreateToken(customer.Id, "stale-consumed", utcNow.AddHours(1));
        staleConsumed.ConsumedAtUtc = utcNow.AddDays(-20).UtcDateTime;
        var staleSuperseded = CreateToken(customer.Id, "stale-superseded", utcNow.AddHours(1));
        staleSuperseded.SupersededAtUtc = utcNow.AddDays(-20).UtcDateTime;
        var recentlyExpired = CreateToken(customer.Id, "recently-expired", utcNow.AddDays(-2));
        var active = CreateToken(customer.Id, "active", utcNow.AddHours(1));
        dbContext.CustomerAccountClaimTokens.AddRange(
            staleExpired,
            staleConsumed,
            staleSuperseded,
            recentlyExpired,
            active);
        await dbContext.SaveChangesAsync();

        var service = new CustomerAccountClaimTokenCleanupService(
            dbContext,
            Options.Create(new AccountClaimSecurityOptions
            {
                TokenRetentionDays = 14,
                CleanupBatchSize = 2
            }),
            new FixedTimeProvider(utcNow));

        var firstDeletedCount = await service.CleanupAsync();
        var secondDeletedCount = await service.CleanupAsync();

        firstDeletedCount.Should().Be(2);
        secondDeletedCount.Should().Be(1);
        dbContext.CustomerAccountClaimTokens.Select(token => token.TokenHash)
            .Should().BeEquivalentTo("recently-expired", "active");
    }

    [Fact]
    public void Model_EnforcesSingleActiveTokenPerCustomer()
    {
        using var dbContext = _dbContextFactory.CreateContext();

        var entityType = dbContext.Model.FindEntityType(typeof(CustomerAccountClaimToken));
        var index = entityType!.GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "ux_customer_account_claim_tokens_one_active");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("consumed_at_utc IS NULL AND superseded_at_utc IS NULL");
    }

    private static CustomerAccountClaimToken CreateToken(Guid customerId, string tokenHash, DateTimeOffset expiresAt) =>
        new()
        {
            CustomerId = customerId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAt.UtcDateTime,
            CreatedAt = expiresAt.AddDays(-1).UtcDateTime,
            UpdatedAt = expiresAt.AddDays(-1).UtcDateTime
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
