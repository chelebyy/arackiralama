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

public sealed class AdminCampaignsControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminCampaignsControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task Create_WhenPayloadValid_CreatesCampaignWithNormalizedCodeAndRestrictions()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var vehicleGroup = await SeedVehicleGroupAsync(dbContext);
        var controller = new AdminCampaignsController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));

        var result = await controller.Create(
            new CreateCampaignRequest(
                " summer25 ",
                "percentage",
                25m,
                2,
                new DateOnly(2026, 6, 1),
                new DateOnly(2026, 8, 31),
                true,
                [vehicleGroup.Id]),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<CampaignDto>>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Code.Should().Be("SUMMER25");
        response.Data.AllowedVehicleGroupIds.Should().ContainSingle().Which.Should().Be(vehicleGroup.Id);
    }

    [Fact]
    public async Task Create_WhenCodeAlreadyExists_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Campaigns.Add(new Campaign
        {
            Code = "SUMMER25",
            DiscountType = "percentage",
            DiscountValue = 25m,
            MinDays = 1,
            ValidFrom = new DateOnly(2026, 6, 1),
            ValidUntil = new DateOnly(2026, 8, 31),
            IsActive = true,
            AllowedVehicleGroupIds = []
        });
        await dbContext.SaveChangesAsync();

        var controller = new AdminCampaignsController(new PricingService(dbContext, new EfUnitOfWork(dbContext)));
        var result = await controller.Create(
            new CreateCampaignRequest(
                "summer25",
                "percentage",
                10m,
                1,
                new DateOnly(2026, 6, 1),
                new DateOnly(2026, 8, 31),
                true,
                []),
            CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("zaten kullaniliyor");
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
