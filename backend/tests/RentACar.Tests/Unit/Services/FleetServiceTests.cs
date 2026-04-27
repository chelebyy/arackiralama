using FluentAssertions;
using Moq;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class FleetServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACarDbContext _dbContext;
    private readonly Mock<IVehiclePhotoStorage> _photoStorageMock = new();
    private readonly FleetService _sut;

    public FleetServiceTests()
    {
        _dbContext = _dbFactory.CreateContext();
        _sut = new FleetService(
            new VehicleGroupRepository(_dbContext),
            new VehicleRepository(_dbContext),
            new OfficeRepository(_dbContext),
            new EfUnitOfWork(_dbContext),
            _photoStorageMock.Object,
            _dbContext);
    }

    [Fact]
    public async Task GetVehicleGroupsAsync_WhenGroupsExist_ReturnsDtos()
    {
        await SeedVehicleGroupAsync("Ekonomi");

        var result = await _sut.GetVehicleGroupsAsync();

        result.Should().HaveCount(1);
        result[0].NameTr.Should().Be("Ekonomi");
    }

    [Fact]
    public async Task CreateVehicleGroupAsync_WhenValid_CreatesWithNormalizedFeatures()
    {
        var request = new CreateVehicleGroupRequest(
            "Ekonomi", "Economy", "Economy", "Economy", "Economy",
            2000m, 21, 2, ["  Klima  ", "Klima", "", "Otomatik"]);

        var result = await _sut.CreateVehicleGroupAsync(request);

        result.NameTr.Should().Be("Ekonomi");
        result.Features.Should().BeEquivalentTo(["Klima", "Otomatik"]);
    }

    [Fact]
    public async Task SearchAvailableVehicleGroupsAsync_WhenNoBlockingReservations_ReturnsGroups()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        await SeedVehicleAsync("34ABC123", group.Id, office.Id);

        var result = await _sut.SearchAvailableVehicleGroupsAsync(
            office.Id, new DateTime(2030, 6, 10, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 6, 12, 10, 0, 0, DateTimeKind.Utc));

        result.Should().HaveCount(1);
        result[0].AvailableCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAvailableVehicleGroupsAsync_WhenBlockedByReservation_ExcludesVehicle()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34ABC123", group.Id, office.Id);
        await SeedReservationAsync(vehicle.Id, new DateTime(2030, 6, 9, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 6, 11, 10, 0, 0, DateTimeKind.Utc), ReservationStatus.Paid);

        var result = await _sut.SearchAvailableVehicleGroupsAsync(
            office.Id, new DateTime(2030, 6, 10, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 6, 12, 10, 0, 0, DateTimeKind.Utc));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAvailableVehicleGroupsAsync_WhenDatesInvalid_ThrowsArgumentException()
    {
        var (office, _) = await SeedOfficeAndGroupAsync();

        var action = () => _sut.SearchAvailableVehicleGroupsAsync(
            office.Id, new DateTime(2030, 6, 12, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 6, 10, 10, 0, 0, DateTimeKind.Utc));

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateVehicleAsync_WhenValid_CreatesVehicle()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var request = new CreateVehicleRequest(
            "34ABC123", "Toyota", "Corolla", 2024, "Beyaz",
            group.Id, office.Id, VehicleStatus.Available);

        var result = await _sut.CreateVehicleAsync(request);

        result.Should().NotBeNull();
        result!.Plate.Should().Be("34ABC123");
        result.Brand.Should().Be("Toyota");
    }

    [Fact]
    public async Task UpdateVehicleAsync_WhenExists_UpdatesVehicle()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34ABC123", group.Id, office.Id);
        var request = new UpdateVehicleRequest(
            "34DEF456", "Honda", "Civic", 2023, "Siyah",
            group.Id, office.Id, VehicleStatus.Available);

        var result = await _sut.UpdateVehicleAsync(vehicle.Id, request);

        result.Should().NotBeNull();
        result!.Plate.Should().Be("34DEF456");
    }

    [Fact]
    public async Task UpdateVehicleAsync_WhenNotExists_ReturnsNull()
    {
        var request = new UpdateVehicleRequest(
            "34DEF456", "Honda", "Civic", 2023, "Siyah",
            Guid.NewGuid(), Guid.NewGuid(), VehicleStatus.Available);

        var result = await _sut.UpdateVehicleAsync(Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteVehicleAsync_WhenExists_DeletesAndCallsPhotoStorage()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34ABC123", group.Id, office.Id);
        vehicle.PhotoUrl = "/photos/1.jpg";
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteVehicleAsync(vehicle.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetOfficesAsync_WhenOfficesExist_ReturnsDtos()
    {
        await SeedOfficeAsync("Alanya Merkez");

        var result = await _sut.GetOfficesAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Alanya Merkez");
    }

    [Fact]
    public async Task CreateOfficeAsync_WhenValid_CreatesOffice()
    {
        var request = new CreateOfficeRequest(
            "Antalya Havalimani", "Havalimani", "+90 242 111 11 11", true, "00:00-23:59");

        var result = await _sut.CreateOfficeAsync(request);

        result.Name.Should().Be("Antalya Havalimani");
        result.IsAirport.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateOfficeAsync_WhenExists_UpdatesOffice()
    {
        var office = await SeedOfficeAsync("Alanya");
        var request = new UpdateOfficeRequest(
            "Alanya Merkez", "Saray Mah.", "+90 242 000 00 00", false, "09:00-18:00");

        var result = await _sut.UpdateOfficeAsync(office.Id, request);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Alanya Merkez");
    }

    [Fact]
    public async Task UpdateOfficeAsync_WhenNotExists_ReturnsNull()
    {
        var request = new UpdateOfficeRequest(
            "X", "Y", "Z", false, "09:00-18:00");

        var result = await _sut.UpdateOfficeAsync(Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IsVehiclePlateAvailableAsync_WhenAvailable_ReturnsTrue()
    {
        var result = await _sut.IsVehiclePlateAvailableAsync("34NEW999");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsVehiclePlateAvailableAsync_WhenTaken_ReturnsFalse()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        await SeedVehicleAsync("34ABC123", group.Id, office.Id);

        var result = await _sut.IsVehiclePlateAvailableAsync("34ABC123");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VehicleGroupExistsAsync_WhenExists_ReturnsTrue()
    {
        var group = await SeedVehicleGroupAsync("Ekonomi");
        var result = await _sut.VehicleGroupExistsAsync(group.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task OfficeExistsAsync_WhenExists_ReturnsTrue()
    {
        var office = await SeedOfficeAsync("Alanya");
        var result = await _sut.OfficeExistsAsync(office.Id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetVehicleByIdAsync_WhenExists_ReturnsVehicle()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34ABC123", group.Id, office.Id);

        var result = await _sut.GetVehicleByIdAsync(vehicle.Id);

        result.Should().NotBeNull();
        result!.Plate.Should().Be("34ABC123");
    }

    [Fact]
    public async Task GetVehicleByIdAsync_WhenNotExists_ReturnsNull()
    {
        var result = await _sut.GetVehicleByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    private async Task<VehicleGroup> SeedVehicleGroupAsync(string name)
    {
        var group = new VehicleGroup
        {
            NameTr = name, NameEn = name, NameRu = name, NameAr = name, NameDe = name,
            DepositAmount = 2000m, MinAge = 21, MinLicenseYears = 2, Features = ["Klima"]
        };
        _dbContext.VehicleGroups.Add(group);
        await _dbContext.SaveChangesAsync();
        return group;
    }

    private async Task<Office> SeedOfficeAsync(string name)
    {
        var office = new Office
        {
            Name = name, Address = "Test", Phone = "+90", IsAirport = false, OpeningHours = "09:00-18:00"
        };
        _dbContext.Offices.Add(office);
        await _dbContext.SaveChangesAsync();
        return office;
    }

    private async Task<(Office office, VehicleGroup group)> SeedOfficeAndGroupAsync()
    {
        var office = await SeedOfficeAsync("Alanya Merkez");
        var group = await SeedVehicleGroupAsync("Ekonomi");
        return (office, group);
    }

    private async Task<Vehicle> SeedVehicleAsync(string plate, Guid groupId, Guid officeId)
    {
        var vehicle = new Vehicle
        {
            Plate = plate, Brand = "Toyota", Model = "Corolla", Year = 2024,
            Color = "Beyaz", GroupId = groupId, OfficeId = officeId, Status = VehicleStatus.Available
        };
        _dbContext.Vehicles.Add(vehicle);
        await _dbContext.SaveChangesAsync();
        return vehicle;
    }

    private async Task<Reservation> SeedReservationAsync(Guid vehicleId, DateTime pickup, DateTime returnDt, ReservationStatus status)
    {
        var reservation = new Reservation
        {
            PublicCode = "R" + Guid.NewGuid().ToString()[..6],
            VehicleId = vehicleId,
            PickupDateTime = pickup,
            ReturnDateTime = returnDt,
            Status = status,
            CustomerId = Guid.NewGuid(),
            TotalAmount = 1000m
        };
        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync();
        return reservation;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }
}
