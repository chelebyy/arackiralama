using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class PricingControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public PricingControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetBreakdown_WhenValidRequest_ReturnsExpectedBreakdown()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            null,
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PriceBreakdownDto>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();

        response.Data!.DailyRate.Should().Be(1100m);
        response.Data.RentalDays.Should().Be(2);
        response.Data.BaseTotal.Should().Be(2200m);
        response.Data.AirportFee.Should().Be(250m);
        response.Data.OneWayFee.Should().Be(500m);
        response.Data.ExtraDriverFee.Should().Be(0m);
        response.Data.ChildSeatFee.Should().Be(0m);
        response.Data.YoungDriverFee.Should().Be(0m);
        response.Data.FullCoverageWaiverFee.Should().Be(0m);
        response.Data.ExtrasTotal.Should().Be(750m);
        response.Data.CampaignDiscount.Should().Be(0m);
        response.Data.FinalTotal.Should().Be(2950m);
        response.Data.DepositAmount.Should().Be(3000m);
        response.Data.PreAuthorizationAmount.Should().Be(3000m);
        response.Data.Currency.Should().Be("TRY");
    }

    [Fact]
    public async Task GetBreakdown_WhenCampaignCodeCaseDiffers_AppliesCampaignDiscount()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: true);

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            "march10",
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PriceBreakdownDto>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();

        response.Data!.CampaignDiscount.Should().Be(295m);
        response.Data.FinalTotal.Should().Be(2655m);
        response.Data.AppliedCampaignCode.Should().Be("MARCH10");
    }

    [Fact]
    public async Task GetBreakdown_WhenCampaignCodeInvalid_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            "unknown-code",
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Gecersiz");
    }

    [Fact]
    public async Task GetBreakdown_WhenCampaignMinDaysNotMet_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);

        dbContext.Campaigns.Add(new Campaign
        {
            Code = "MIN3",
            DiscountType = "percentage",
            DiscountValue = 10m,
            MinDays = 3,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            IsActive = true,
            AllowedVehicleGroupIds = []
        });

        await dbContext.SaveChangesAsync();

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            "MIN3",
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Gecersiz");
    }

    [Fact]
    public async Task GetBreakdown_WhenHigherPrioritySeasonalRuleExists_UsesPriorityOrder()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);

        dbContext.PricingRules.Add(new PricingRule
        {
            VehicleGroupId = vehicleGroup.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            DailyPrice = 1500m,
            Multiplier = 1m,
            WeekdayMultiplier = 1m,
            WeekendMultiplier = 1m,
            CalculationType = "fixed",
            Priority = 10
        });
        await dbContext.SaveChangesAsync();

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            null,
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PriceBreakdownDto>>().Subject;

        response.Data.Should().NotBeNull();
        response.Data!.DailyRate.Should().Be(1500m);
        response.Data.BaseTotal.Should().Be(3000m);
    }

    [Fact]
    public async Task GetBreakdown_WhenAdditionalFeesAndWaiverSelected_ReturnsDetailedFeeBreakdown()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);
        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            null,
            2,
            1,
            23,
            true,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PriceBreakdownDto>>().Subject;

        response.Data.Should().NotBeNull();
        response.Data!.ExtraDriverFee.Should().Be(300m);
        response.Data.ChildSeatFee.Should().Be(150m);
        response.Data.YoungDriverFee.Should().Be(200m);
        response.Data.FullCoverageWaiverFee.Should().Be(700m);
        response.Data.ExtrasTotal.Should().Be(2100m);
        response.Data.DepositAmount.Should().Be(0m);
        response.Data.PreAuthorizationAmount.Should().Be(0m);
        response.Data.FinalTotal.Should().Be(4300m);
    }

    [Fact]
    public async Task GetBreakdown_WhenWeekendMultiplierConfigured_AppliesWeekendPricing()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);

        var pricingRule = await dbContext.PricingRules.FirstAsync();
        pricingRule.WeekdayMultiplier = 1m;
        pricingRule.WeekendMultiplier = 1.2m;
        await dbContext.SaveChangesAsync();

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));
        var pickupDate = new DateTime(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);
        var returnDate = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            pickupDate,
            returnDate,
            null,
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PriceBreakdownDto>>().Subject;

        response.Data.Should().NotBeNull();
        response.Data!.DailyRate.Should().Be(1100m);
        response.Data.BaseTotal.Should().Be(2420m);
        response.Data.FinalTotal.Should().Be(3170m);
    }

    [Fact]
    public async Task GetBreakdown_WhenCampaignRestrictedToDifferentVehicleGroup_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: false);

        var otherVehicleGroup = new VehicleGroup
        {
            NameTr = "SUV",
            NameEn = "SUV",
            NameRu = "SUV",
            NameAr = "SUV",
            NameDe = "SUV",
            DepositAmount = 5000m,
            MinAge = 25,
            MinLicenseYears = 3,
            Features = ["4x4"]
        };
        dbContext.VehicleGroups.Add(otherVehicleGroup);
        await dbContext.SaveChangesAsync();

        dbContext.Campaigns.Add(new Campaign
        {
            Code = "SUVONLY",
            DiscountType = "percentage",
            DiscountValue = 15m,
            MinDays = 1,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            IsActive = true,
            AllowedVehicleGroupIds = [otherVehicleGroup.Id]
        });
        await dbContext.SaveChangesAsync();

        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            "SUVONLY",
            0,
            0,
            null,
            false,
            CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Gecersiz");
    }

    [Fact]
    public async Task GetBreakdown_WhenInvoked_CompletesUnderOneHundredMilliseconds_OnWarmPathAverage()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (vehicleGroup, pickupOffice, returnOffice) = await SeedPricingDataAsync(dbContext, useCampaign: true);
        var controller = new PricingController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var warmupResult = await controller.GetBreakdown(
            vehicleGroup.Id,
            pickupOffice.Id,
            returnOffice.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            "MARCH10",
            1,
            1,
            24,
            false,
            CancellationToken.None);

        warmupResult.Should().BeOfType<OkObjectResult>();

        const int iterationCount = 5;
        var totalElapsedMilliseconds = 0L;

        for (var i = 0; i < iterationCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await controller.GetBreakdown(
                vehicleGroup.Id,
                pickupOffice.Id,
                returnOffice.Id,
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(3),
                "MARCH10",
                1,
                1,
                24,
                false,
                CancellationToken.None);
            stopwatch.Stop();

            result.Should().BeOfType<OkObjectResult>();
            totalElapsedMilliseconds += stopwatch.ElapsedMilliseconds;
        }

        var averageElapsedMilliseconds = totalElapsedMilliseconds / iterationCount;
        averageElapsedMilliseconds.Should().BeLessThan(100);
    }

    private static async Task<(VehicleGroup VehicleGroup, Office PickupOffice, Office ReturnOffice)> SeedPricingDataAsync(
        RentACar.Infrastructure.Data.RentACarDbContext dbContext,
        bool useCampaign)
    {
        var vehicleGroup = new VehicleGroup
        {
            NameTr = "Ekonomi",
            NameEn = "Economy",
            NameRu = "Economy",
            NameAr = "Economy",
            NameDe = "Economy",
            DepositAmount = 3000m,
            MinAge = 21,
            MinLicenseYears = 2,
            Features = ["Klima"]
        };

        var pickupOffice = new Office
        {
            Name = "Havalimani",
            Address = "Alanya Airport",
            Phone = "+90 242 000 00 00",
            IsAirport = true,
            OpeningHours = "00:00-23:59"
        };

        var returnOffice = new Office
        {
            Name = "Merkez",
            Address = "Alanya Merkez",
            Phone = "+90 242 000 00 01",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
        };

        dbContext.VehicleGroups.Add(vehicleGroup);
        dbContext.Offices.AddRange(pickupOffice, returnOffice);
        await dbContext.SaveChangesAsync();

        dbContext.PricingRules.Add(new PricingRule
        {
            VehicleGroupId = vehicleGroup.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            DailyPrice = 1000m,
            Multiplier = 1.1m,
            WeekdayMultiplier = 1m,
            WeekendMultiplier = 1m,
            CalculationType = "multiplier",
            Priority = 0
        });

        if (useCampaign)
        {
            dbContext.Campaigns.Add(new Campaign
            {
                Code = "MARCH10",
                DiscountType = "percentage",
                DiscountValue = 10m,
                MinDays = 1,
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                IsActive = true,
                AllowedVehicleGroupIds = []
            });
        }

        await dbContext.SaveChangesAsync();

        return (vehicleGroup, pickupOffice, returnOffice);
    }
}
