using System.IO;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
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

public sealed class AdminVehiclesControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminVehiclesControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetAll_WhenVehicleExists_ReturnsVehicles()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        dbContext.Vehicles.Add(new Vehicle
        {
            Plate = "07ABC001",
            Brand = "Toyota",
            Model = "Corolla",
            Year = 2023,
            Color = "White",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<VehicleDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Data![0].Plate.Should().Be("07ABC001");
    }

    [Fact]
    public async Task Create_WhenPlateAlreadyExists_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        dbContext.Vehicles.Add(new Vehicle
        {
            Plate = "07ABC001",
            Brand = "Toyota",
            Model = "Corolla",
            Year = 2023,
            Color = "White",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new CreateVehicleRequest(
            Plate: "07ABC001",
            Brand: "Renault",
            Model: "Clio",
            Year: 2022,
            Color: "Black",
            GroupId: groupId,
            OfficeId: officeId,
            Status: VehicleStatus.Available);

        var result = await controller.Create(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WhenValidRequest_ReturnsCreatedVehicle()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var controller = CreateController(dbContext);
        var request = new CreateVehicleRequest(
            Plate: "07ABC999",
            Brand: "Renault",
            Model: "Clio",
            Year: 2022,
            Color: "Black",
            GroupId: groupId,
            OfficeId: officeId,
            Status: VehicleStatus.Available);

        var result = await controller.Create(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Plate.Should().Be("07ABC999");
        response.Data.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenVehicleExists_ReturnsOk()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var vehicle = new Vehicle
        {
            Plate = "07DEL001",
            Brand = "Opel",
            Model = "Corsa",
            Year = 2021,
            Color = "Blue",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Delete(vehicle.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        dbContext.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateStatus_WhenVehicleExists_UpdatesStatus()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var vehicle = new Vehicle
        {
            Plate = "07STS001",
            Brand = "Fiat",
            Model = "Egea",
            Year = 2022,
            Color = "Gray",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.UpdateStatus(vehicle.Id, new UpdateVehicleStatusRequest(VehicleStatus.OutOfService), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Status.Should().Be(VehicleStatus.OutOfService);
    }

    [Fact]
    public async Task UpdateStatus_WhenVehicleExists_WritesAuditLog()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var vehicle = new Vehicle
        {
            Plate = "07AUD001",
            Brand = "Ford",
            Model = "Focus",
            Year = 2022,
            Color = "Black",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, userId: "admin-1");
        await controller.UpdateStatus(vehicle.Id, new UpdateVehicleStatusRequest(VehicleStatus.Maintenance), CancellationToken.None);

        var auditLog = dbContext.AuditLogs.Should().ContainSingle(log => log.Action == "VehicleStatusUpdated").Subject;
        auditLog.EntityType.Should().Be(nameof(Vehicle));
        auditLog.EntityId.Should().Be(vehicle.Id.ToString());
        auditLog.UserId.Should().Be("admin-1");
        auditLog.Details.Should().Contain("Available");
        auditLog.Details.Should().Contain("Maintenance");
    }

    [Fact]
    public async Task Transfer_WhenTargetOfficeMissing_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var vehicle = new Vehicle
        {
            Plate = "07TRF001",
            Brand = "Hyundai",
            Model = "i20",
            Year = 2021,
            Color = "Red",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new TransferVehicleRequest(Guid.NewGuid(), VehicleStatus.Available);

        var result = await controller.Transfer(vehicle.Id, request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Transfer_WhenValidRequest_UpdatesOffice()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var targetOffice = new Office
        {
            Name = "Havalimani",
            Code = "gazipasa-havalimani",
            Address = "Gazipasa Havalimani",
            Phone = "+90 242 111 11 11",
            IsAirport = true,
            OpeningHours = "00:00-23:59"
        };
        dbContext.Offices.Add(targetOffice);

        var vehicle = new Vehicle
        {
            Plate = "07TRF002",
            Brand = "Volkswagen",
            Model = "Polo",
            Year = 2020,
            Color = "White",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new TransferVehicleRequest(targetOffice.Id, VehicleStatus.Available);

        var result = await controller.Transfer(vehicle.Id, request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.OfficeId.Should().Be(targetOffice.Id);
    }

    [Fact]
    public async Task ScheduleMaintenance_WhenValidRequest_SetsMaintenanceStatus()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var vehicle = new Vehicle
        {
            Plate = "07MNT001",
            Brand = "Peugeot",
            Model = "301",
            Year = 2021,
            Color = "Silver",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new ScheduleVehicleMaintenanceRequest(
            StartDateUtc: DateTime.UtcNow.AddHours(1),
            EndDateUtc: DateTime.UtcNow.AddDays(2),
            Notes: "Periyodik kontrol");

        var result = await controller.ScheduleMaintenance(vehicle.Id, request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Status.Should().Be(VehicleStatus.Maintenance);
    }

    [Fact]
    public async Task UploadPhoto_WhenExtensionInvalid_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);

        var vehicle = new Vehicle
        {
            Plate = "07IMG001",
            Brand = "Skoda",
            Model = "Fabia",
            Year = 2021,
            Color = "Green",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        using var stream = new MemoryStream([1, 2, 3, 4]);
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "vehicle.gif");

        var controller = CreateController(dbContext);
        var result = await controller.UploadPhoto(vehicle.Id, file, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadPhoto_WhenValidFile_SavesPhotoAndReturnsVehicle()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var (groupId, officeId) = await SeedGroupAndOfficeAsync(dbContext);
        var storageRoot = Path.Combine(Path.GetTempPath(), "rentacar-photo-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var vehicle = new Vehicle
            {
                Plate = "07IMG002",
                Brand = "Citroen",
                Model = "C3",
                Year = 2022,
                Color = "Orange",
                GroupId = groupId,
                OfficeId = officeId,
                Status = VehicleStatus.Available
            };
            dbContext.Vehicles.Add(vehicle);
            await dbContext.SaveChangesAsync();

            using var stream = new MemoryStream([10, 20, 30, 40, 50]);
            IFormFile file = new FormFile(stream, 0, stream.Length, "file", "vehicle.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var controller = CreateController(dbContext, storageRoot);
            var result = await controller.UploadPhoto(vehicle.Id, file, CancellationToken.None);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<VehicleDto>>().Subject;
            response.Success.Should().BeTrue();
            response.Data.Should().NotBeNull();
            response.Data!.PhotoUrl.Should().NotBeNullOrWhiteSpace();

            var savedPath = Path.Combine(storageRoot, response.Data.PhotoUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            File.Exists(savedPath).Should().BeTrue();
            dbContext.Vehicles.Single(v => v.Id == vehicle.Id).PhotoUrl.Should().Be(response.Data.PhotoUrl);
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    private static AdminVehiclesController CreateController(RentACarDbContext dbContext, string? webRootPath = null, string? userId = null)
    {
        var httpContext = userId is null ? null : CreateAuthenticatedHttpContext(userId);
        var httpContextAccessor = httpContext is null ? null : new HttpContextAccessor { HttpContext = httpContext };
        var fleetService = CreateFleetService(dbContext, webRootPath, httpContextAccessor);
        var controller = new AdminVehiclesController(fleetService, Mock.Of<IAuditLogService>());

        if (httpContext is not null)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        return controller;
    }

    private static IFleetService CreateFleetService(
        RentACarDbContext dbContext,
        string? webRootPath = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var vehicleGroupRepository = new VehicleGroupRepository(dbContext);
        var vehicleRepository = new VehicleRepository(dbContext);
        var officeRepository = new OfficeRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var photoStorage = new LocalVehiclePhotoStorage(webRootPath ?? Path.Combine(Path.GetTempPath(), "rentacar-test-wwwroot"));

        return new FleetService(
            vehicleGroupRepository,
            vehicleRepository,
            officeRepository,
            unitOfWork,
            photoStorage,
            dbContext,
            httpContextAccessor);
    }

    private static DefaultHttpContext CreateAuthenticatedHttpContext(string userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
            new Claim(ClaimTypes.Role, "Admin")
        };

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
    }

    private static async Task<(Guid GroupId, Guid OfficeId)> SeedGroupAndOfficeAsync(RentACarDbContext dbContext)
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
            MinLicenseYears = 2
        };

        var office = new Office
        {
            Name = "Merkez Ofis",
            Code = "merkez-ofis",
            Address = "Saray Mah. Ataturk Cad. No:1",
            Phone = "+90 242 000 00 00",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
        };

        dbContext.VehicleGroups.Add(vehicleGroup);
        dbContext.Offices.Add(office);
        await dbContext.SaveChangesAsync();

        return (vehicleGroup.Id, office.Id);
    }
}
