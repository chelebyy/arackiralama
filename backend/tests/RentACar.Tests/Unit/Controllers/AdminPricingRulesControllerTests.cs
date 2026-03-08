using FluentAssertions;
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

public sealed class AdminPricingRulesControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminPricingRulesControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetAll_WhenPricingRulesExist_ReturnsOk()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var vehicleGroup = await SeedVehicleGroupAsync(dbContext);
        dbContext.PricingRules.Add(new PricingRule
        {
            VehicleGroupId = vehicleGroup.Id,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 8, 31),
            DailyPrice = 1800m,
            Multiplier = 1m,
            WeekdayMultiplier = 1m,
            WeekendMultiplier = 1m,
            CalculationType = "fixed",
            Priority = 10
        });
        await dbContext.SaveChangesAsync();

        var controller = new AdminPricingRulesController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));
        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<PricingRuleDto>>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Data![0].CalculationType.Should().Be("fixed");
    }

    [Fact]
    public async Task Create_WhenOverlappingRuleWithSamePriorityExists_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var vehicleGroup = await SeedVehicleGroupAsync(dbContext);
        dbContext.PricingRules.Add(new PricingRule
        {
            VehicleGroupId = vehicleGroup.Id,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 8, 31),
            DailyPrice = 1800m,
            Multiplier = 1m,
            WeekdayMultiplier = 1m,
            WeekendMultiplier = 1m,
            CalculationType = "fixed",
            Priority = 5
        });
        await dbContext.SaveChangesAsync();

        var controller = new AdminPricingRulesController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));
        var result = await controller.Create(
            new CreatePricingRuleRequest(vehicleGroup.Id, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 1500m, 1m, 1m, 1m, "fixed", 5),
            CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("cakisan");
    }

    private static async Task<VehicleGroup> SeedVehicleGroupAsync(RentACarDbContext dbContext)
    {
        var vehicleGroup = new VehicleGroup
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

        dbContext.VehicleGroups.Add(vehicleGroup);
        await dbContext.SaveChangesAsync();
        return vehicleGroup;
    }
}
