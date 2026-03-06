using FluentAssertions;
using RentACar.Core.Entities;
using RentACar.Core.Specifications;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Integration.Data;

public sealed class FleetRepositoryStandardsTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public FleetRepositoryStandardsTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task VehicleRepository_ListAsync_WithSpecification_FiltersAndOrdersVehicles()
    {
        using var dbContext = _dbContextFactory.CreateContext();

        var officeA = new Office
        {
            Name = "Merkez Ofis",
            Address = "Adres 1",
            Phone = "+90 242 000 00 01",
            OpeningHours = "09:00-18:00"
        };
        var officeB = new Office
        {
            Name = "Havalimani",
            Address = "Adres 2",
            Phone = "+90 242 000 00 02",
            IsAirport = true,
            OpeningHours = "00:00-23:59"
        };
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

        dbContext.Offices.AddRange(officeA, officeB);
        dbContext.VehicleGroups.Add(vehicleGroup);
        await dbContext.SaveChangesAsync();

        dbContext.Vehicles.AddRange(
            new Vehicle
            {
                Plate = "07BBB002",
                Brand = "Renault",
                Model = "Clio",
                Year = 2023,
                Color = "Blue",
                GroupId = vehicleGroup.Id,
                OfficeId = officeA.Id
            },
            new Vehicle
            {
                Plate = "07AAA001",
                Brand = "Fiat",
                Model = "Egea",
                Year = 2022,
                Color = "White",
                GroupId = vehicleGroup.Id,
                OfficeId = officeA.Id
            },
            new Vehicle
            {
                Plate = "07CCC003",
                Brand = "Opel",
                Model = "Corsa",
                Year = 2021,
                Color = "Gray",
                GroupId = vehicleGroup.Id,
                OfficeId = officeB.Id
            });
        await dbContext.SaveChangesAsync();

        var repository = new VehicleRepository(dbContext);

        var vehicles = await repository.ListAsync(new VehiclesByOfficeSpecification(officeA.Id), CancellationToken.None);

        vehicles.Select(static vehicle => vehicle.Plate).Should().Equal("07AAA001", "07BBB002");
    }

    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_PersistsPendingRepositoryChanges()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new OfficeRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var office = new Office
        {
            Name = "Yeni Ofis",
            Address = "Adres",
            Phone = "+90 242 000 00 03",
            OpeningHours = "09:00-18:00"
        };

        await repository.AddAsync(office, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        dbContext.Offices.Should().ContainSingle(existingOffice => existingOffice.Name == "Yeni Ofis");
    }

    private sealed class VehiclesByOfficeSpecification : Specification<Vehicle>
    {
        public VehiclesByOfficeSpecification(Guid officeId)
        {
            ApplyCriteria(vehicle => vehicle.OfficeId == officeId);
            ApplyOrderBy(query => query.OrderBy(vehicle => vehicle.Plate));
        }
    }
}
