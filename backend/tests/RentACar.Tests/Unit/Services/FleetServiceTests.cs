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
            2000m, 21, 2, true, ["  Klima  ", "Klima", "", "Otomatik"]);

        var result = await _sut.CreateVehicleGroupAsync(request);

        result.NameTr.Should().Be("Ekonomi");
        result.IsActive.Should().BeTrue();
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
    public async Task SearchAvailableVehicleGroupsAsync_UsesPickupDatePricing()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        await SeedVehicleAsync("34FUT123", group.Id, office.Id);
        var pickup = new DateTime(2099, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        _dbContext.PricingRules.Add(new PricingRule
        {
            VehicleGroupId = group.Id,
            StartDate = new DateOnly(2099, 6, 1),
            EndDate = new DateOnly(2099, 6, 30),
            DailyPrice = 1200m,
            CalculationType = "fixed"
        });
        await _dbContext.SaveChangesAsync();
        var result = await _sut.SearchAvailableVehicleGroupsAsync(office.Id, pickup, pickup.AddDays(3));
        result.Single().DailyPrice.Should().Be(1200m);
    }

    [Fact]
    public async Task SearchAvailableVehicleGroupsAsync_UsesTurkeyDateAtPricingBoundary()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        await SeedVehicleAsync("34EAR123", group.Id, office.Id);
        var pickupUtc = new DateTime(2030, 5, 9, 22, 0, 0, DateTimeKind.Utc);
        _dbContext.PricingRules.Add(new PricingRule
        {
            VehicleGroupId = group.Id,
            StartDate = new DateOnly(2030, 5, 10),
            EndDate = new DateOnly(2030, 5, 13),
            DailyPrice = 1200m,
            CalculationType = "fixed"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.SearchAvailableVehicleGroupsAsync(office.Id, pickupUtc, pickupUtc.AddDays(3));

        result.Single().DailyPrice.Should().Be(1200m);
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
    public async Task SearchAvailableVehicleGroupsAsync_WhenBlockedByUnpaidRequest_ExcludesVehicle()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34UNP123", group.Id, office.Id);
        await SeedReservationAsync(vehicle.Id, new DateTime(2030, 6, 9, 10, 0, 0, DateTimeKind.Utc), new DateTime(2030, 6, 11, 10, 0, 0, DateTimeKind.Utc), ReservationStatus.UnpaidRequest);

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

    [Theory]
    [InlineData(ReservationStatus.Draft)]
    [InlineData(ReservationStatus.Hold)]
    [InlineData(ReservationStatus.PendingPayment)]
    [InlineData(ReservationStatus.Paid)]
    [InlineData(ReservationStatus.Active)]
    [InlineData(ReservationStatus.UnpaidRequest)]
    [InlineData(ReservationStatus.Confirmed)]
    public async Task UpdateVehicleAsync_PreservesGroupForActiveSnapshotlessReservation(ReservationStatus status)
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34LEG123", group.Id, office.Id);
        vehicle.RentalTerms = new VehicleRentalTerms { DepositAmount = 9000m, MinAge = 23, MinLicenseYears = 3 };
        await SeedReservationAsync(vehicle.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(5), status);
        var auditCount = _dbContext.AuditLogs.Count();
        foreach (var groupId in new Guid?[] { null, Guid.NewGuid() })
        {
            var request = new UpdateVehicleRequest("34NEW123", "Changed", "Changed", 2023, "Black", groupId, office.Id, VehicleStatus.Available);
            var action = () => _sut.UpdateVehicleAsync(vehicle.Id, request);
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*active reservations depend*");
            vehicle.GroupId.Should().Be(group.Id);
            vehicle.Plate.Should().Be("34LEG123");
            _dbContext.AuditLogs.Count().Should().Be(auditCount);
        }

        var unchangedGroup = new UpdateVehicleRequest("34LEG123", "Updated", "Corolla", 2024, "White", group.Id, office.Id, VehicleStatus.Available);
        (await _sut.UpdateVehicleAsync(vehicle.Id, unchangedGroup))!.Brand.Should().Be("Updated");
    }

    [Theory]
    [InlineData(ReservationStatus.Completed, false)]
    [InlineData(ReservationStatus.Cancelled, false)]
    [InlineData(ReservationStatus.Expired, false)]
    [InlineData(ReservationStatus.Confirmed, true)]
    public async Task UpdateVehicleAsync_AllowsGroupRemovalWithoutActiveSnapshotDependency(ReservationStatus status, bool hasSnapshot)
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34SAFE123", group.Id, office.Id);
        vehicle.RentalTerms = new VehicleRentalTerms { DepositAmount = 9000m, MinAge = 23, MinLicenseYears = 3 };
        var reservation = await SeedReservationAsync(vehicle.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(5), status);
        if (hasSnapshot) reservation.PricingSnapshot = new ReservationPricingSnapshotV1 { DepositAmount = 2000m };
        await _dbContext.SaveChangesAsync();

        var request = new UpdateVehicleRequest(vehicle.Plate, vehicle.Brand, vehicle.Model, vehicle.Year, vehicle.Color, null, office.Id, VehicleStatus.Available);
        (await _sut.UpdateVehicleAsync(vehicle.Id, request))!.GroupId.Should().BeNull();
        if (hasSnapshot) reservation.PricingSnapshot!.DepositAmount.Should().Be(2000m);
    }

    [Fact]
    public async Task DeleteVehicleAsync_WhenExists_DeletesAndCallsPhotoStorage()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34ABC123", group.Id, office.Id);
        vehicle.PhotoUrl = "/photos/1.jpg";
        await _dbContext.SaveChangesAsync();

        var result = await _sut.DeleteVehicleAsync(vehicle.Id);

        result.Should().Be(VehicleDeletionOutcome.Deleted);
    }

    [Fact]
    public async Task DeleteVehicleAsync_WhenVehicleHasReservation_ArchivesAndKeepsVehicle()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var vehicle = await SeedVehicleAsync("34DEL999", group.Id, office.Id);
        await SeedReservationAsync(
            vehicle.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            ReservationStatus.Paid);

        var result = await _sut.DeleteVehicleAsync(vehicle.Id);

        result.Should().Be(VehicleDeletionOutcome.Archived);
        _dbContext.Vehicles.Should().ContainSingle(x => x.Id == vehicle.Id && x.Status == VehicleStatus.Retired);
        _photoStorageMock.Verify(x => x.DeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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
            "ayt", "Antalya Havalimani", "Havalimani", "+90 242 111 11 11", true, true, "00:00-23:59");

        var result = await _sut.CreateOfficeAsync(request);

        result.Code.Should().Be("ayt");
        result.Name.Should().Be("Antalya Havalimani");
        result.IsAirport.Should().BeTrue();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateOfficeAsync_WhenExists_UpdatesOffice()
    {
        var office = await SeedOfficeAsync("Alanya");
        var request = new UpdateOfficeRequest(
            "ala", "Alanya Merkez", "Saray Mah.", "+90 242 000 00 00", false, false, "09:00-18:00");

        var result = await _sut.UpdateOfficeAsync(office.Id, request);

        result.Should().NotBeNull();
        result!.Code.Should().Be("ala");
        result!.Name.Should().Be("Alanya Merkez");
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateOfficeAsync_WhenNotExists_ReturnsNull()
    {
        var request = new UpdateOfficeRequest(
            "x", "X", "Y", "Z", false, true, "09:00-18:00");

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
            NameTr = name,
            NameEn = name,
            NameRu = name,
            NameAr = name,
            NameDe = name,
            DepositAmount = 2000m,
            MinAge = 21,
            MinLicenseYears = 2,
            Features = ["Klima"]
        };
        _dbContext.VehicleGroups.Add(group);
        await _dbContext.SaveChangesAsync();
        return group;
    }

    private async Task<Office> SeedOfficeAsync(string name)
    {
        var office = new Office
        {
            Name = name,
            Code = string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant(),
            Address = "Test",
            Phone = "+90",
            IsAirport = false,
            OpeningHours = "09:00-18:00"
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

[Fact]
    public async Task CatalogueUpdate_PreservesOtherVehiclesAndLegacyRelations()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var first = await SeedVehicleAsync("34CAT001", group.Id, office.Id);
        var second = await SeedVehicleAsync("34CAT002", group.Id, office.Id);
        first.PhotoUrl = "/uploads/vehicles/legacy.png";
        await _dbContext.SaveChangesAsync();
        var updated = await _sut.UpdateVehicleAsync(first.Id, new UpdateVehicleRequest(
            first.Plate, first.Brand, first.Model, first.Year, first.Color, group.Id, office.Id, first.Status,
            "manual", "diesel", 4, 1, "sedan", 4, "1.5", 110, ["bluetooth"]));
        updated!.SeatCount.Should().Be(4);
        updated.PhotoUrls.Should().Equal("/uploads/vehicles/legacy.png");
        updated.GroupId.Should().Be(group.Id);
        updated.Equipment.Should().Equal("bluetooth");
        var other = await _sut.GetVehicleByIdAsync(second.Id);
        other!.SeatCount.Should().BeNull();
        other.Transmission.Should().BeNull();
        other.PhotoUrls.Should().BeEmpty();
        var publicController = new RentACar.API.Controllers.VehiclesController(_sut, _dbContext);
        var response = (Microsoft.AspNetCore.Mvc.OkObjectResult)await publicController.GetById(first.Id, CancellationToken.None);
        var data = ((RentACar.API.Contracts.ApiResponse<PublicVehicleDto>)response.Value!).Data!;
        data.Transmission.Should().Be("manual");
        data.Features.Should().Equal("bluetooth");
        data.DailyPrice.Should().BeNull();
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        json.Should().NotContain("Plate").And.NotContain("34CAT001").And.NotContain("Maintenance");
    }

    [Fact]
    public async Task CatalogueGallery_PreservesSharedFilesAndRejectsForeignReferences()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var first = await SeedVehicleAsync("34GAL001", group.Id, office.Id);
        var second = await SeedVehicleAsync("34GAL002", group.Id, office.Id);
        first.PhotoUrls = ["/uploads/vehicles/shared.png", "/uploads/vehicles/own.png"];
        first.PhotoUrl = first.PhotoUrls[0];
        second.PhotoUrl = first.PhotoUrl;
        await _dbContext.SaveChangesAsync();
        var reordered = await _sut.UpdateVehiclePhotosAsync(first.Id, first.PhotoUrls.Reverse().ToArray());
        reordered!.PhotoUrl.Should().Be("/uploads/vehicles/own.png");
        await _sut.UpdateVehiclePhotosAsync(first.Id, ["/uploads/vehicles/own.png"]);
        second.PhotoUrl.Should().Be("/uploads/vehicles/shared.png");
        _photoStorageMock.Verify(storage => storage.DeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        var foreign = () => _sut.UpdateVehiclePhotosAsync(first.Id, ["/uploads/vehicles/foreign.png"]);
        await foreign.Should().ThrowAsync<ArgumentException>();
        var duplicate = () => _sut.UpdateVehiclePhotosAsync(first.Id, ["/uploads/vehicles/own.png", "/uploads/vehicles/own.png"]);
        await duplicate.Should().ThrowAsync<ArgumentException>();
        var empty = await _sut.UpdateVehiclePhotosAsync(first.Id, []);
        empty!.PhotoUrl.Should().BeNull();
        empty.PhotoUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task CatalogueUpload_AppendsRatherThanReplacingLegacyPhoto()
    {
        var (office, group) = await SeedOfficeAndGroupAsync();
        var first = await SeedVehicleAsync("34UPL001", group.Id, office.Id);
        first.PhotoUrl = "/uploads/vehicles/legacy.png";
        await _dbContext.SaveChangesAsync();
        _photoStorageMock.Setup(storage => storage.SaveAsync(first.Id, It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/vehicles/new.png");
        var result = await _sut.UploadVehiclePhotoAsync(first.Id, Mock.Of<Microsoft.AspNetCore.Http.IFormFile>());
        result!.PhotoUrls.Should().Equal("/uploads/vehicles/legacy.png", "/uploads/vehicles/new.png");
        result.PhotoUrl.Should().Be("/uploads/vehicles/legacy.png");
        _photoStorageMock.Verify(storage => storage.DeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<Vehicle> SeedVehicleAsync(string plate, Guid groupId, Guid officeId)
    {
        var vehicle = new Vehicle
        {
            Plate = plate,
            Brand = "Toyota",
            Model = "Corolla",
            Year = 2024,
            Color = "Beyaz",
            GroupId = groupId,
            OfficeId = officeId,
            Status = VehicleStatus.Available
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
