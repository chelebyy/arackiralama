using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PricingServiceTests : IDisposable
{
    private static readonly DateTime FixedPickup = new(2030, 6, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedReturn = new(2030, 6, 12, 10, 0, 0, DateTimeKind.Utc);

    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACarDbContext _dbContext;
    private readonly PricingService _sut;

    public PricingServiceTests()
    {
        _dbContext = _dbFactory.CreateContext();
        _sut = new PricingService(_dbContext, new EfUnitOfWork(_dbContext));
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenVehicleGroupNotFound_ReturnsNull()
    {
        var result = await _sut.CalculateBreakdownAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            FixedPickup, FixedReturn, null, 0, 0, null, false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenPricingRuleNotFound_ReturnsNull()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, null, false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenBasicScenario_ReturnsCorrectDailyRateAndBaseTotal()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, null, false);

        result.Should().NotBeNull();
        result!.DailyRate.Should().Be(500m);
        result.RentalDays.Should().Be(2);
        result.BaseTotal.Should().Be(1000m);
        result.FinalTotal.Should().Be(1000m);
        result.DepositAmount.Should().Be(group.DepositAmount);
        result.Currency.Should().Be("TRY");
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenAirportOffice_ReturnsAirportFee()
    {
        var (group, _, returnOffice) = await SeedBasicDataAsync();
        returnOffice.IsAirport = true;
        await _dbContext.SaveChangesAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, returnOffice.Id, returnOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, null, false);

        result!.AirportFee.Should().Be(250m);
        result.ExtrasTotal.Should().Be(250m);
        result.FinalTotal.Should().Be(1250m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenOneWay_ReturnsOneWayFee()
    {
        var (group, pickupOffice, returnOffice) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, returnOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, null, false);

        result!.OneWayFee.Should().Be(500m);
        result.ExtrasTotal.Should().Be(750m);
        result.FinalTotal.Should().Be(1750m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenExtraDriver_ReturnsExtraDriverFee()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 2, 0, null, false);

        result!.ExtraDriverFee.Should().Be(300m);
        result.ExtrasTotal.Should().Be(300m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenChildSeat_ReturnsChildSeatFee()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 0, 1, null, false);

        result!.ChildSeatFee.Should().Be(150m);
        result.ExtrasTotal.Should().Be(150m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenYoungDriver_ReturnsYoungDriverFee()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, 22, false);

        result!.YoungDriverFee.Should().Be(200m);
        result.ExtrasTotal.Should().Be(200m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenNotYoungDriver_ReturnsZeroYoungDriverFee()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, 30, false);

        result!.YoungDriverFee.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenFullCoverageWaiver_ReturnsZeroDeposit()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, null, 0, 0, null, true);

        result!.DepositAmount.Should().Be(0m);
        result.FullCoverageWaiverFee.Should().Be(700m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenCampaignPercentage_ReturnsDiscount()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 1000m, FixedPickup, FixedReturn, 1);
        await SeedCampaignAsync("SAVE10", "percentage", 10m, 1, FixedPickup, [group.Id]);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, "SAVE10", 0, 0, null, false);

        result!.CampaignDiscount.Should().Be(200m);
        result.FinalTotal.Should().Be(1800m);
        result.AppliedCampaignCode.Should().Be("SAVE10");
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenCampaignFixed_ReturnsDiscount()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 1000m, FixedPickup, FixedReturn, 1);
        await SeedCampaignAsync("SAVE500", "fixed", 500m, 1, FixedPickup, [group.Id]);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, "SAVE500", 0, 0, null, false);

        result!.CampaignDiscount.Should().Be(500m);
        result.FinalTotal.Should().Be(1500m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenCampaignDiscountExceedsSubtotal_ClampsToSubtotal()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 100m, FixedPickup, FixedReturn, 1);
        await SeedCampaignAsync("BIGSAVE", "fixed", 9999m, 1, FixedPickup, [group.Id]);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, "BIGSAVE", 0, 0, null, false);

        result!.CampaignDiscount.Should().Be(200m);
        result.FinalTotal.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateBreakdownAsync_WhenCampaignInvalid_ReturnsNoDiscount()
    {
        var (group, pickupOffice, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 1000m, FixedPickup, FixedReturn, 1);

        var result = await _sut.CalculateBreakdownAsync(
            group.Id, pickupOffice.Id, pickupOffice.Id,
            FixedPickup, FixedReturn, "INVALID", 0, 0, null, false);

        result!.CampaignDiscount.Should().Be(0m);
        result.AppliedCampaignCode.Should().BeNull();
    }

    [Fact]
    public async Task IsCampaignCodeAvailableAsync_WhenAvailable_ReturnsTrue()
    {
        var result = await _sut.IsCampaignCodeAvailableAsync("NEWCODE");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCampaignCodeAvailableAsync_WhenUnavailable_ReturnsFalse()
    {
        await SeedCampaignAsync("TAKEN", "percentage", 10m, 1, FixedPickup, []);

        var result = await _sut.IsCampaignCodeAvailableAsync("TAKEN");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCampaignCodeAvailableAsync_WhenExcludingSelf_ReturnsTrue()
    {
        var campaign = await SeedCampaignAsync("TAKEN", "percentage", 10m, 1, FixedPickup, []);

        var result = await _sut.IsCampaignCodeAvailableAsync("TAKEN", campaign.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCampaignCodeAvailableAsync_WhenEmptyCode_ReturnsFalse()
    {
        var result = await _sut.IsCampaignCodeAvailableAsync("");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCampaignCodeValidAsync_WhenValid_ReturnsTrue()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        await SeedCampaignAsync("VALID", "percentage", 10m, 1, FixedPickup, [group.Id]);

        var result = await _sut.IsCampaignCodeValidAsync("VALID", group.Id, 2, DateOnly.FromDateTime(FixedPickup));
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCampaignCodeValidAsync_WhenCodeNotFound_ReturnsFalse()
    {
        var (group, _, _) = await SeedBasicDataAsync();

        var result = await _sut.IsCampaignCodeValidAsync("MISSING", group.Id, 2, DateOnly.FromDateTime(FixedPickup));
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasConflictingPricingRuleAsync_WhenConflictExists_ReturnsTrue()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.HasConflictingPricingRuleAsync(
            group.Id, DateOnly.FromDateTime(FixedPickup), DateOnly.FromDateTime(FixedReturn), 1);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasConflictingPricingRuleAsync_WhenNoConflict_ReturnsFalse()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.HasConflictingPricingRuleAsync(
            group.Id, DateOnly.FromDateTime(FixedReturn).AddDays(1), DateOnly.FromDateTime(FixedReturn).AddDays(3), 1);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasConflictingPricingRuleAsync_WhenExcludingSelf_ReturnsFalse()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        var rule = await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.HasConflictingPricingRuleAsync(
            group.Id, DateOnly.FromDateTime(FixedPickup), DateOnly.FromDateTime(FixedReturn), 1, rule.Id);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VehicleGroupExistsAsync_WhenExists_ReturnsTrue()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        var result = await _sut.VehicleGroupExistsAsync(group.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VehicleGroupExistsAsync_WhenNotExists_ReturnsFalse()
    {
        var result = await _sut.VehicleGroupExistsAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VehicleGroupsExistAsync_WhenEmpty_ReturnsTrue()
    {
        var result = await _sut.VehicleGroupsExistAsync([]);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task OfficeExistsAsync_WhenExists_ReturnsTrue()
    {
        var (_, office, _) = await SeedBasicDataAsync();
        var result = await _sut.OfficeExistsAsync(office.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task OfficeExistsAsync_WhenNotExists_ReturnsFalse()
    {
        var result = await _sut.OfficeExistsAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPricingRulesAsync_WhenRulesExist_ReturnsRules()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        await SeedPricingRuleAsync(group.Id, 500m, FixedPickup, FixedReturn, 1);

        var result = await _sut.GetPricingRulesAsync();
        result.Should().HaveCount(1);
        result[0].VehicleGroupId.Should().Be(group.Id);
    }

    [Fact]
    public async Task CreatePricingRuleAsync_WhenValid_CreatesRule()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        var request = new CreatePricingRuleRequest(
            group.Id,
            DateOnly.FromDateTime(FixedPickup),
            DateOnly.FromDateTime(FixedReturn),
            600m,
            1m,
            1m,
            1m,
            "multiplier",
            1);

        var result = await _sut.CreatePricingRuleAsync(request);

        result.VehicleGroupId.Should().Be(group.Id);
        result.DailyPrice.Should().Be(600m);
    }

    [Fact]
    public async Task GetCampaignsAsync_WhenCampaignsExist_ReturnsCampaigns()
    {
        await SeedCampaignAsync("C1", "percentage", 10m, 1, FixedPickup, []);

        var result = await _sut.GetCampaignsAsync();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateCampaignAsync_WhenValid_CreatesCampaign()
    {
        var (group, _, _) = await SeedBasicDataAsync();
        var request = new CreateCampaignRequest("NEWCAMP", "fixed", 100m, 1, DateOnly.FromDateTime(FixedPickup), DateOnly.FromDateTime(FixedReturn), true, [group.Id]);

        var result = await _sut.CreateCampaignAsync(request);

        result.Code.Should().Be("NEWCAMP");
        result.DiscountType.Should().Be("fixed");
    }

    [Fact]
    public async Task UpdateCampaignAsync_WhenExists_UpdatesCampaign()
    {
        var campaign = await SeedCampaignAsync("OLD", "percentage", 10m, 1, FixedPickup, []);
        var request = new UpdateCampaignRequest("NEWNAME", "fixed", 50m, 2, DateOnly.FromDateTime(FixedPickup), DateOnly.FromDateTime(FixedReturn), true, []);

        var result = await _sut.UpdateCampaignAsync(campaign.Id, request);

        result.Should().NotBeNull();
        result!.Code.Should().Be("NEWNAME");
        result.DiscountType.Should().Be("fixed");
    }

    [Fact]
    public async Task UpdateCampaignAsync_WhenNotExists_ReturnsNull()
    {
        var request = new UpdateCampaignRequest("X", "fixed", 50m, 2, DateOnly.FromDateTime(FixedPickup), DateOnly.FromDateTime(FixedReturn), true, []);
        var result = await _sut.UpdateCampaignAsync(Guid.NewGuid(), request);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCampaignAsync_WhenExists_ReturnsTrue()
    {
        var campaign = await SeedCampaignAsync("DEL", "percentage", 10m, 1, FixedPickup, []);
        var result = await _sut.DeleteCampaignAsync(campaign.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCampaignAsync_WhenNotExists_ReturnsFalse()
    {
        var result = await _sut.DeleteCampaignAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("percentage", true)]
    [InlineData("fixed", true)]
    [InlineData("invalid", false)]
    public void IsSupportedCampaignDiscountType_ReturnsExpected(string type, bool expected)
    {
        PricingService.IsSupportedCampaignDiscountType(type).Should().Be(expected);
    }

    [Theory]
    [InlineData("multiplier", true)]
    [InlineData("fixed", true)]
    [InlineData("invalid", false)]
    public void IsSupportedPricingCalculationType_ReturnsExpected(string type, bool expected)
    {
        PricingService.IsSupportedPricingCalculationType(type).Should().Be(expected);
    }

    private async Task<(VehicleGroup group, Office pickup, Office returnOffice)> SeedBasicDataAsync()
    {
        var group = new VehicleGroup
        {
            NameTr = "Ekonomi",
            NameEn = "Economy",
            NameRu = "Economy",
            NameAr = "Economy",
            NameDe = "Economy",
            DepositAmount = 2000m,
            MinAge = 21,
            MinLicenseYears = 2,
            Features = ["Klima"]
        };
        var pickupOffice = new Office
        {
            Name = "Alanya Merkez",
            Code = "alanya-merkez",
            Address = "Saray Mah.",
            Phone = "+90 242 000 00 00",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
        };
        var returnOffice = new Office
        {
            Name = "Antalya Havalimani",
            Code = "antalya-havalimani",
            Address = "Havalimani",
            Phone = "+90 242 111 11 11",
            IsAirport = true,
            OpeningHours = "00:00-23:59"
        };

        _dbContext.VehicleGroups.Add(group);
        _dbContext.Offices.Add(pickupOffice);
        _dbContext.Offices.Add(returnOffice);
        await _dbContext.SaveChangesAsync();
        return (group, pickupOffice, returnOffice);
    }

    private async Task<PricingRule> SeedPricingRuleAsync(Guid groupId, decimal dailyRate, DateTime start, DateTime end, int priority)
    {
        var rule = new PricingRule
        {
            VehicleGroupId = groupId,
            StartDate = DateOnly.FromDateTime(start),
            EndDate = DateOnly.FromDateTime(end),
            DailyPrice = dailyRate,
            Multiplier = 1m,
            WeekdayMultiplier = 1m,
            WeekendMultiplier = 1m,
            Priority = priority,
            CalculationType = "multiplier"
        };
        _dbContext.PricingRules.Add(rule);
        await _dbContext.SaveChangesAsync();
        return rule;
    }

    private async Task<Campaign> SeedCampaignAsync(string code, string discountType, decimal discountValue, int minDays, DateTime validFrom, List<Guid> allowedGroups)
    {
        var campaign = new Campaign
        {
            Code = code,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MinDays = minDays,
            ValidFrom = DateOnly.FromDateTime(validFrom),
            ValidUntil = DateOnly.FromDateTime(validFrom).AddMonths(1),
            IsActive = true,
            AllowedVehicleGroupIds = allowedGroups
        };
        _dbContext.Campaigns.Add(campaign);
        await _dbContext.SaveChangesAsync();
        return campaign;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }
}
