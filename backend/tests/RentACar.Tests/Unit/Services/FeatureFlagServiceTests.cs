using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Data;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class FeatureFlagServiceTests
{
    private const string OnlinePaymentDescription =
        "Online ödeme seçeneklerini public rezervasyon akışında gösterir. Kapalıyken müşteriler ödeme yapmadan 24 saat stok bloklu talep oluşturur.";

    [Fact]
    public async Task GetAllAsync_WhenOnlinePaymentFlagHasOldDescription_UpdatesBusinessDescription()
    {
        await using var dbContext = CreateDbContext();
        dbContext.FeatureFlags.Add(new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Name = "EnableOnlinePayment",
            Enabled = false,
            Description = "Online payment provider integration toggle",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();
        var service = new FeatureFlagService(dbContext);

        var flags = await service.GetAllAsync(CancellationToken.None);

        flags.Should().ContainSingle(x =>
            x.Name == "EnableOnlinePayment" &&
            x.Description == OnlinePaymentDescription);
        dbContext.FeatureFlags.Single(x => x.Name == "EnableOnlinePayment")
            .Description.Should().Be(OnlinePaymentDescription);
    }

    [Fact]
    public async Task GetAllAsync_WhenRequiredFlagsAreMissing_CreatesOnlinePaymentFlagDisabled()
    {
        await using var dbContext = CreateDbContext();
        var service = new FeatureFlagService(dbContext);

        var flags = await service.GetAllAsync(CancellationToken.None);

        flags.Should().Contain(x =>
            x.Name == "EnableOnlinePayment" &&
            !x.Enabled &&
            x.Description == OnlinePaymentDescription);
    }

    private static RentACarDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RentACarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RentACarDbContext(options);
    }
}
