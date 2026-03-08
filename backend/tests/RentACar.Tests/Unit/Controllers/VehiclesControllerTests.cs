using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class VehiclesControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public VehiclesControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetAvailable_WhenVehiclesBlockedByReservationOrMaintenance_ExcludesThem()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (group, office) = await SeedGroupAndOfficeAsync(dbContext, "Merkez Ofis");

        var availableVehicle = CreateVehicle("07AVL001", group.Id, office.Id, VehicleStatus.Available);
        var reservedVehicle = CreateVehicle("07AVL002", group.Id, office.Id, VehicleStatus.Available);
        var maintenanceVehicle = CreateVehicle("07AVL003", group.Id, office.Id, VehicleStatus.Maintenance);

        dbContext.Vehicles.AddRange(availableVehicle, reservedVehicle, maintenanceVehicle);
        dbContext.Reservations.Add(new Reservation
        {
            PublicCode = "RSV-001",
            CustomerId = Guid.NewGuid(),
            VehicleId = reservedVehicle.Id,
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            Status = ReservationStatus.Paid,
            TotalAmount = 1000
        });
        await dbContext.SaveChangesAsync();

        var controller = new VehiclesController(CreateFleetService(dbContext));
        var result = await controller.GetAvailable(
            office.Id,
            DateTime.UtcNow.AddDays(1).AddHours(1),
            DateTime.UtcNow.AddDays(2),
            null,
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<AvailableVehicleGroupDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().ContainSingle();
        response.Data![0].GroupId.Should().Be(group.Id);
        response.Data[0].AvailableCount.Should().Be(1);
    }

    [Fact]
    public async Task Transfer_WhenVehicleMovesOffice_UpdatesAvailabilityInventory()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (group, sourceOffice) = await SeedGroupAndOfficeAsync(dbContext, "Merkez Ofis");
        var targetOffice = new Office
        {
            Name = "Havalimani",
            Address = "Gazipasa Havalimani",
            Phone = "+90 242 111 11 11",
            IsAirport = true,
            OpeningHours = "00:00-23:59"
        };
        dbContext.Offices.Add(targetOffice);

        var vehicle = CreateVehicle("07TRF900", group.Id, sourceOffice.Id, VehicleStatus.Available);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var vehiclesController = new VehiclesController(CreateFleetService(dbContext));
        var adminController = new AdminVehiclesController(CreateFleetService(dbContext));
        var pickup = DateTime.UtcNow.AddDays(1);
        var dropoff = DateTime.UtcNow.AddDays(2);

        var sourceBefore = await vehiclesController.GetAvailable(sourceOffice.Id, pickup, dropoff, null, CancellationToken.None);
        GetAvailableGroups(sourceBefore).Should().ContainSingle(groupDto => groupDto.GroupId == group.Id && groupDto.AvailableCount == 1);

        await adminController.Transfer(vehicle.Id, new TransferVehicleRequest(targetOffice.Id, VehicleStatus.Available), CancellationToken.None);

        var sourceAfter = await vehiclesController.GetAvailable(sourceOffice.Id, pickup, dropoff, null, CancellationToken.None);
        var targetAfter = await vehiclesController.GetAvailable(targetOffice.Id, pickup, dropoff, null, CancellationToken.None);

        GetAvailableGroups(sourceAfter).Should().BeEmpty();
        GetAvailableGroups(targetAfter).Should().ContainSingle(groupDto => groupDto.GroupId == group.Id && groupDto.AvailableCount == 1);
    }

    private static IReadOnlyList<AvailableVehicleGroupDto> GetAvailableGroups(IActionResult actionResult)
    {
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<AvailableVehicleGroupDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        return response.Data!;
    }

    private static IFleetService CreateFleetService(RentACarDbContext dbContext)
    {
        var vehicleGroupRepository = new VehicleGroupRepository(dbContext);
        var vehicleRepository = new VehicleRepository(dbContext);
        var officeRepository = new OfficeRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var photoStorage = new LocalVehiclePhotoStorage(Path.Combine(Path.GetTempPath(), "rentacar-test-wwwroot"));

        return new FleetService(
            vehicleGroupRepository,
            vehicleRepository,
            officeRepository,
            unitOfWork,
            photoStorage,
            dbContext);
    }

    private static Vehicle CreateVehicle(string plate, Guid groupId, Guid officeId, VehicleStatus status) =>
        new()
        {
            Plate = plate,
            Brand = "Renault",
            Model = "Clio",
            Year = 2023,
            Color = "White",
            GroupId = groupId,
            OfficeId = officeId,
            Status = status
        };

    private static async Task<(VehicleGroup Group, Office Office)> SeedGroupAndOfficeAsync(RentACarDbContext dbContext, string officeName)
    {
        var vehicleGroup = new VehicleGroup
        {
            NameTr = "Ekonomi",
            NameEn = "Economy",
            NameRu = "Economy",
            NameAr = "Economy",
            NameDe = "Economy",
            DepositAmount = 2000,
            MinAge = 21,
            MinLicenseYears = 2,
            Features = ["Klima", "Otomatik"]
        };

        var office = new Office
        {
            Name = officeName,
            Address = $"{officeName} Adres",
            Phone = "+90 242 000 00 00",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
        };

        dbContext.VehicleGroups.Add(vehicleGroup);
        dbContext.Offices.Add(office);
        await dbContext.SaveChangesAsync();

        return (vehicleGroup, office);
    }
}
