using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Controllers;
using RentACar.API.Services;
using RentACar.Core.Interfaces;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminOfficesControllerTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public AdminOfficesControllerTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetAll_WhenOfficeExists_ReturnsOffices()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Offices.Add(new Office
        {
            Name = "Merkez Ofis",
            Address = "Saray Mah. Ataturk Cad. No:1",
            Phone = "+90 242 000 00 00",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<IReadOnlyList<OfficeDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Data![0].Name.Should().Be("Merkez Ofis");
    }

    [Fact]
    public async Task Create_WhenRequestInvalid_ReturnsBadRequest()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var controller = CreateController(dbContext);

        var request = new CreateOfficeRequest(
            Name: string.Empty,
            Address: "Saray Mah. Ataturk Cad. No:1",
            Phone: "+90 242 000 00 00",
            IsAirport: false,
            OpeningHours: "09:00-18:00");

        var result = await controller.Create(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WhenOfficeExists_UpdatesOffice()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var office = new Office
        {
            Name = "Merkez Ofis",
            Address = "Saray Mah. Ataturk Cad. No:1",
            Phone = "+90 242 000 00 00",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
        };
        dbContext.Offices.Add(office);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new UpdateOfficeRequest(
            Name: "Havalimani Ofisi",
            Address: "Gazipasa Havalimani",
            Phone: "+90 242 111 11 11",
            IsAirport: true,
            OpeningHours: "07:00-23:00");

        var result = await controller.Update(office.Id, request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<OfficeDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be("Havalimani Ofisi");
        response.Data.IsAirport.Should().BeTrue();
    }

    private static AdminOfficesController CreateController(RentACarDbContext dbContext)
    {
        var vehicleGroupRepository = new VehicleGroupRepository(dbContext);
        var vehicleRepository = new VehicleRepository(dbContext);
        var officeRepository = new OfficeRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var photoStorage = new LocalVehiclePhotoStorage(Path.Combine(Path.GetTempPath(), "rentacar-test-wwwroot"));
        var fleetService = new FleetService(vehicleGroupRepository, vehicleRepository, officeRepository, unitOfWork, photoStorage, dbContext);
        return new AdminOfficesController(fleetService);
    }
}




