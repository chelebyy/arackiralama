using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminVehicleGroupsControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminVehicleGroupsControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetAll_WhenVehicleGroupExists_ReturnsVehicleGroups()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.VehicleGroups.Add(new()
        {
            NameTr = "Ekonomi",
            NameEn = "Economy",
            NameRu = "Economy",
            NameAr = "Economy",
            NameDe = "Economy",
            DepositAmount = 2000,
            MinAge = 21,
            MinLicenseYears = 2,
            Features = ["AirConditioning"]
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<VehicleGroupDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Data![0].NameTr.Should().Be("Ekonomi");
    }

    [Fact]
    public async Task Create_WhenRequestInvalid_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var request = new CreateVehicleGroupRequest(
            NameTr: string.Empty,
            NameEn: "Economy",
            NameRu: "Economy",
            NameAr: "Economy",
            NameDe: "Economy",
            DepositAmount: 1000,
            MinAge: 21,
            MinLicenseYears: 2,
            Features: null);

        var result = await controller.Create(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WhenValidRequest_ReturnsCreatedVehicleGroup()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var request = new CreateVehicleGroupRequest(
            NameTr: "Premium",
            NameEn: "Premium",
            NameRu: "Premium",
            NameAr: "Premium",
            NameDe: "Premium",
            DepositAmount: 4500,
            MinAge: 25,
            MinLicenseYears: 3,
            Features: ["LeatherSeats", "Navigation"]);

        var result = await controller.Create(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleGroupDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.NameTr.Should().Be("Premium");
        response.Data.Features.Should().BeEquivalentTo(["LeatherSeats", "Navigation"]);
    }

    [Fact]
    public async Task Update_WhenVehicleGroupDoesNotExist_ReturnsNotFound()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var request = new UpdateVehicleGroupRequest(
            NameTr: "SUV",
            NameEn: "SUV",
            NameRu: "SUV",
            NameAr: "SUV",
            NameDe: "SUV",
            DepositAmount: 3000,
            MinAge: 24,
            MinLicenseYears: 2,
            Features: ["BackupCamera"]);

        var result = await controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_WhenVehicleGroupExists_UpdatesVehicleGroup()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var vehicleGroup = new Core.Entities.VehicleGroup
        {
            NameTr = "Ekonomi",
            NameEn = "Economy",
            NameRu = "Economy",
            NameAr = "Economy",
            NameDe = "Economy",
            DepositAmount = 2000,
            MinAge = 21,
            MinLicenseYears = 2
        };

        dbContext.VehicleGroups.Add(vehicleGroup);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new UpdateVehicleGroupRequest(
            NameTr: "Luks",
            NameEn: "Luxury",
            NameRu: "Luxury",
            NameAr: "Luxury",
            NameDe: "Luxury",
            DepositAmount: 5500,
            MinAge: 27,
            MinLicenseYears: 4,
            Features: ["Sunroof", "LeatherSeats"]);

        var result = await controller.Update(vehicleGroup.Id, request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleGroupDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.NameTr.Should().Be("Luks");
        response.Data.Features.Should().BeEquivalentTo(["Sunroof", "LeatherSeats"]);
    }

    private static AdminVehicleGroupsController CreateController(RentACarDbContext dbContext)
    {
        var vehicleGroupRepository = new VehicleGroupRepository(dbContext);
        var vehicleRepository = new VehicleRepository(dbContext);
        var officeRepository = new OfficeRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var photoStorage = new LocalVehiclePhotoStorage(Path.Combine(Path.GetTempPath(), "rentacar-test-wwwroot"));
        var fleetService = new FleetService(vehicleGroupRepository, vehicleRepository, officeRepository, unitOfWork, photoStorage);
        return new AdminVehicleGroupsController(fleetService);
    }
}



