using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Repositories;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Integration.Data;

public sealed class ReservationRepositoryTests : IClassFixture<TestDbContextFactory>
{
    private readonly TestDbContextFactory _dbContextFactory;

    public ReservationRepositoryTests(TestDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Fact]
    public async Task GetByPublicCodeAsync_WhenReservationExists_ReturnsReservationWithDetails()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var office = new Office { Name = "Test Office", Address = "Test Address", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
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
        var vehicle = new Vehicle
        {
            Plate = "07XYZ001",
            Brand = "Renault",
            Model = "Clio",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };
        var customer = new Customer
        {
            FullName = "Ahmet Yilmaz",
            Email = "ahmet@example.com",
            Phone = "+90 555 123 4567"
        };
        var reservation = new Reservation
        {
            PublicCode = "RES-12345",
            Customer = customer,
            Vehicle = vehicle,
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(3),
            Status = ReservationStatus.Paid,
            TotalAmount = 1500
        };

        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);
        dbContext.Customers.Add(customer);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByPublicCodeAsync("RES-12345");

        result.Should().NotBeNull();
        result!.PublicCode.Should().Be("RES-12345");
        result.Customer.Should().NotBeNull();
        result.Customer!.Email.Should().Be("ahmet@example.com");
        result.Vehicle.Should().NotBeNull();
        result.Vehicle!.Brand.Should().Be("Renault");
    }

    [Fact]
    public async Task GetByCustomerIdAsync_ReturnsCustomerReservations()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer
        {
            FullName = "Mehmet Test",
            Email = "mehmet@test.com",
            Phone = "+90 555 987 6543"
        };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07ABC001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var reservations = new[]
        {
            new Reservation { PublicCode = "RES-001", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(1), ReturnDateTime = DateTime.UtcNow.AddDays(3), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "RES-002", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(5), ReturnDateTime = DateTime.UtcNow.AddDays(7), Status = ReservationStatus.Draft, TotalAmount = 1200 },
        };

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync();

        var results = await repository.GetByCustomerIdAsync(customer.Id);

        results.Should().HaveCount(2);
        results.Select(r => r.PublicCode).Should().Contain("RES-001", "RES-002");
    }

    [Fact]
    public async Task GetActiveReservationsForVehicleAsync_ReturnsOnlyActiveStatuses()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var reservations = new[]
        {
            new Reservation { PublicCode = "ACTIVE-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(1), ReturnDateTime = DateTime.UtcNow.AddDays(2), Status = ReservationStatus.Hold, TotalAmount = 1000 },
            new Reservation { PublicCode = "ACTIVE-2", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(3), ReturnDateTime = DateTime.UtcNow.AddDays(4), Status = ReservationStatus.PendingPayment, TotalAmount = 1000 },
            new Reservation { PublicCode = "ACTIVE-3", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(5), ReturnDateTime = DateTime.UtcNow.AddDays(6), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "ACTIVE-4", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(7), ReturnDateTime = DateTime.UtcNow.AddDays(8), Status = ReservationStatus.Active, TotalAmount = 1000 },
            new Reservation { PublicCode = "CANCELLED-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(9), ReturnDateTime = DateTime.UtcNow.AddDays(10), Status = ReservationStatus.Cancelled, TotalAmount = 1000 },
            new Reservation { PublicCode = "COMPLETED-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(-5), ReturnDateTime = DateTime.UtcNow.AddDays(-3), Status = ReservationStatus.Completed, TotalAmount = 1000 },
            new Reservation { PublicCode = "DRAFT-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(11), ReturnDateTime = DateTime.UtcNow.AddDays(12), Status = ReservationStatus.Draft, TotalAmount = 1000 },
        };

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync();

        var results = await repository.GetActiveReservationsForVehicleAsync(vehicle.Id);

        results.Should().HaveCount(4);
        results.Select(r => r.PublicCode).Should().Contain("ACTIVE-1", "ACTIVE-2", "ACTIVE-3", "ACTIVE-4");
        results.Select(r => r.PublicCode).Should().NotContain("CANCELLED-1", "COMPLETED-1", "DRAFT-1");
    }

    [Fact]
    public async Task HasOverlappingReservationsAsync_WhenOverlappingExists_ReturnsTrue()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var existingReservation = new Reservation
        {
            PublicCode = "EXISTING",
            Customer = customer,
            Vehicle = vehicle,
            PickupDateTime = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Paid,
            TotalAmount = 1000
        };

        dbContext.Reservations.Add(existingReservation);
        await dbContext.SaveChangesAsync();

        var overlapStart = new DateTime(2026, 3, 16, 10, 0, 0, DateTimeKind.Utc);
        var overlapEnd = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);

        var hasOverlap = await repository.HasOverlappingReservationsAsync(vehicle.Id, overlapStart, overlapEnd);

        hasOverlap.Should().BeTrue();
    }

    [Fact]
    public async Task HasOverlappingReservationsAsync_WhenNoOverlap_ReturnsFalse()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var existingReservation = new Reservation
        {
            PublicCode = "EXISTING",
            Customer = customer,
            Vehicle = vehicle,
            PickupDateTime = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Paid,
            TotalAmount = 1000
        };

        dbContext.Reservations.Add(existingReservation);
        await dbContext.SaveChangesAsync();

        var noOverlapStart = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
        var noOverlapEnd = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

        var hasOverlap = await repository.HasOverlappingReservationsAsync(vehicle.Id, noOverlapStart, noOverlapEnd);

        hasOverlap.Should().BeFalse();
    }

    [Fact]
    public async Task GetReservationsByStatusAsync_ReturnsOnlyMatchingStatus()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var reservations = new[]
        {
            new Reservation { PublicCode = "PAID-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(1), ReturnDateTime = DateTime.UtcNow.AddDays(2), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "PAID-2", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(3), ReturnDateTime = DateTime.UtcNow.AddDays(4), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "DRAFT-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(5), ReturnDateTime = DateTime.UtcNow.AddDays(6), Status = ReservationStatus.Draft, TotalAmount = 1000 },
        };

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync();

        var results = await repository.GetReservationsByStatusAsync(ReservationStatus.Paid);

        results.Should().HaveCount(2);
        results.All(r => r.Status == ReservationStatus.Paid).Should().BeTrue();
    }

    [Fact]
    public async Task GetExpiredReservationsAsync_ReturnsOnlyExpiredHolds()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var cutoffDate = DateTime.UtcNow.AddMinutes(-15);

        var reservations = new[]
        {
            new Reservation { PublicCode = "EXPIRED-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(1), ReturnDateTime = DateTime.UtcNow.AddDays(2), Status = ReservationStatus.Hold, TotalAmount = 1000, CreatedAt = cutoffDate.AddMinutes(-5) },
            new Reservation { PublicCode = "EXPIRED-2", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(3), ReturnDateTime = DateTime.UtcNow.AddDays(4), Status = ReservationStatus.Hold, TotalAmount = 1000, CreatedAt = cutoffDate.AddMinutes(-10) },
            new Reservation { PublicCode = "FRESH-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(5), ReturnDateTime = DateTime.UtcNow.AddDays(6), Status = ReservationStatus.Hold, TotalAmount = 1000, CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new Reservation { PublicCode = "PAID-1", Customer = customer, Vehicle = vehicle, PickupDateTime = DateTime.UtcNow.AddDays(7), ReturnDateTime = DateTime.UtcNow.AddDays(8), Status = ReservationStatus.Paid, TotalAmount = 1000, CreatedAt = cutoffDate.AddMinutes(-20) },
        };

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync();

        var results = await repository.GetExpiredReservationsAsync(cutoffDate);

        results.Should().HaveCount(2);
        results.Select(r => r.PublicCode).Should().Contain("EXPIRED-1", "EXPIRED-2");
        results.Select(r => r.PublicCode).Should().NotContain("FRESH-1", "PAID-1");
    }

    [Fact]
    public async Task SearchReservationsAsync_WithMultipleFilters_ReturnsFilteredResults()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer1 = new Customer { FullName = "Customer 1", Email = "c1@test.com", Phone = "+90 555 000 0001" };
        var customer2 = new Customer { FullName = "Customer 2", Email = "c2@test.com", Phone = "+90 555 000 0002" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle1 = new Vehicle { Plate = "07V1", Brand = "Test", Model = "Test", Group = group, Office = office, Status = VehicleStatus.Available };
        var vehicle2 = new Vehicle { Plate = "07V2", Brand = "Test", Model = "Test", Group = group, Office = office, Status = VehicleStatus.Available };

        dbContext.Customers.AddRange(customer1, customer2);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.AddRange(vehicle1, vehicle2);

        var baseDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var reservations = new[]
        {
            new Reservation { PublicCode = "SEARCH-1", Customer = customer1, Vehicle = vehicle1, PickupDateTime = baseDate.AddDays(5), ReturnDateTime = baseDate.AddDays(7), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "SEARCH-2", Customer = customer1, Vehicle = vehicle2, PickupDateTime = baseDate.AddDays(10), ReturnDateTime = baseDate.AddDays(12), Status = ReservationStatus.Draft, TotalAmount = 1000 },
            new Reservation { PublicCode = "SEARCH-3", Customer = customer2, Vehicle = vehicle1, PickupDateTime = baseDate.AddDays(15), ReturnDateTime = baseDate.AddDays(17), Status = ReservationStatus.Paid, TotalAmount = 1000 },
        };

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync();

        var results = await repository.SearchReservationsAsync(
            customerId: customer1.Id,
            status: ReservationStatus.Paid,
            fromDate: baseDate.AddDays(4),
            toDate: baseDate.AddDays(8));

        results.Should().HaveCount(1);
        results.First().PublicCode.Should().Be("SEARCH-1");
    }

    [Fact]
    public async Task GetByVehicleIdAsync_ReturnsVehicleReservationsOrderedByPickupDate()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var baseDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var reservations = new[]
        {
            new Reservation { PublicCode = "LATER", Customer = customer, Vehicle = vehicle, PickupDateTime = baseDate.AddDays(10), ReturnDateTime = baseDate.AddDays(12), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "EARLIER", Customer = customer, Vehicle = vehicle, PickupDateTime = baseDate.AddDays(5), ReturnDateTime = baseDate.AddDays(7), Status = ReservationStatus.Paid, TotalAmount = 1000 },
            new Reservation { PublicCode = "LATEST", Customer = customer, Vehicle = vehicle, PickupDateTime = baseDate.AddDays(15), ReturnDateTime = baseDate.AddDays(17), Status = ReservationStatus.Paid, TotalAmount = 1000 },
        };

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync();

        var results = await repository.GetByVehicleIdAsync(vehicle.Id);

        results.Should().HaveCount(3);
        results.Select(r => r.PublicCode).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task HasOverlappingReservationsAsync_WithExcludeReservationId_ExcludesSpecifiedReservation()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Test User", Email = "test@test.com", Phone = "+90 555 000 0000" };
        var office = new Office { Name = "Office", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07TEST001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var existingReservation = new Reservation
        {
            PublicCode = "EXISTING",
            Customer = customer,
            Vehicle = vehicle,
            PickupDateTime = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            ReturnDateTime = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Paid,
            TotalAmount = 1000
        };

        dbContext.Reservations.Add(existingReservation);
        await dbContext.SaveChangesAsync();

        var overlapStart = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var overlapEnd = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Utc);

        var hasOverlapWhenExcluded = await repository.HasOverlappingReservationsAsync(
            vehicle.Id, overlapStart, overlapEnd, excludeReservationId: existingReservation.Id);

        hasOverlapWhenExcluded.Should().BeFalse();

        var hasOverlapWhenNotExcluded = await repository.HasOverlappingReservationsAsync(
            vehicle.Id, overlapStart, overlapEnd);

        hasOverlapWhenNotExcluded.Should().BeTrue();
    }

    [Fact]
    public async Task GetExpiredReservationsAsync_WhenReservationCreatedAtEqualsCutoff_ExcludesBoundaryReservation()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Boundary User", Email = "boundary@test.com", Phone = "+90 555 000 0100" };
        var office = new Office { Name = "Office", Code = "office-boundary", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07BOUND001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);

        var cutoffDate = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        dbContext.Reservations.AddRange(
            new Reservation
            {
                PublicCode = "OLDER-HOLD",
                Customer = customer,
                Vehicle = vehicle,
                PickupDateTime = cutoffDate.AddDays(1),
                ReturnDateTime = cutoffDate.AddDays(2),
                Status = ReservationStatus.Hold,
                TotalAmount = 1000,
                CreatedAt = cutoffDate.AddMinutes(-1)
            },
            new Reservation
            {
                PublicCode = "BOUNDARY-HOLD",
                Customer = customer,
                Vehicle = vehicle,
                PickupDateTime = cutoffDate.AddDays(3),
                ReturnDateTime = cutoffDate.AddDays(4),
                Status = ReservationStatus.Hold,
                TotalAmount = 1000,
                CreatedAt = cutoffDate
            });

        await dbContext.SaveChangesAsync();

        var results = await repository.GetExpiredReservationsAsync(cutoffDate);

        results.Select(r => r.PublicCode).Should().ContainSingle().Which.Should().Be("OLDER-HOLD");
    }

    [Fact]
    public async Task SearchReservationsAsync_WhenDateRangeMatchesBoundaries_IncludesReservation()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Boundary Search", Email = "search@test.com", Phone = "+90 555 000 0200" };
        var office = new Office { Name = "Office", Code = "office-search", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07SRCH001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };
        var pickup = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc);
        var dropoff = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);
        dbContext.Reservations.Add(new Reservation
        {
            PublicCode = "BOUNDARY-SEARCH",
            Customer = customer,
            Vehicle = vehicle,
            PickupDateTime = pickup,
            ReturnDateTime = dropoff,
            Status = ReservationStatus.Paid,
            TotalAmount = 1200
        });

        await dbContext.SaveChangesAsync();

        var results = await repository.SearchReservationsAsync(
            fromDate: pickup,
            toDate: dropoff,
            page: 1,
            pageSize: 20);

        results.Select(r => r.PublicCode).Should().ContainSingle().Which.Should().Be("BOUNDARY-SEARCH");
    }

    [Fact]
    public async Task SearchReservationsAsync_WhenRequestedPageIsBeyondResultSet_ReturnsEmptyCollection()
    {
        using var dbContext = _dbContextFactory.CreateContext();
        var repository = new ReservationRepository(dbContext);

        var customer = new Customer { FullName = "Paging User", Email = "paging@test.com", Phone = "+90 555 000 0300" };
        var office = new Office { Name = "Office", Code = "office-paging", Address = "Addr", Phone = "+90 555 000 0000" };
        var group = new VehicleGroup
        {
            NameTr = "Test",
            NameEn = "Test",
            NameRu = "Test",
            NameAr = "Test",
            NameDe = "Test",
            DepositAmount = 1000,
            MinAge = 21,
            MinLicenseYears = 2
        };
        var vehicle = new Vehicle
        {
            Plate = "07PAGE001",
            Brand = "Test",
            Model = "Test",
            Group = group,
            Office = office,
            Status = VehicleStatus.Available
        };

        dbContext.Customers.Add(customer);
        dbContext.Offices.Add(office);
        dbContext.VehicleGroups.Add(group);
        dbContext.Vehicles.Add(vehicle);
        dbContext.Reservations.Add(new Reservation
        {
            PublicCode = "PAGE-ONLY",
            Customer = customer,
            Vehicle = vehicle,
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2),
            Status = ReservationStatus.Paid,
            TotalAmount = 1000
        });

        await dbContext.SaveChangesAsync();

        var results = await repository.SearchReservationsAsync(page: 3, pageSize: 1);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task HasOverlappingReservationsAsync_WhenReservationIsCreatedInParallelContext_DetectsOverlap()
    {
        var databaseName = $"ReservationOverlap_{Guid.NewGuid():N}";

        await using (var setupContext = CreateSharedContext(databaseName))
        {
            var office = new Office { Name = "Office", Code = "office-parallel", Address = "Addr", Phone = "+90 555 000 0000" };
            var group = new VehicleGroup
            {
                NameTr = "Test",
                NameEn = "Test",
                NameRu = "Test",
                NameAr = "Test",
                NameDe = "Test",
                DepositAmount = 1000,
                MinAge = 21,
                MinLicenseYears = 2
            };
            var vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                Plate = "07PAR001",
                Brand = "Test",
                Model = "Test",
                Group = group,
                Office = office,
                Status = VehicleStatus.Available
            };
            var customer = new Customer { FullName = "Parallel User", Email = "parallel@test.com", Phone = "+90 555 000 0400" };

            setupContext.Offices.Add(office);
            setupContext.VehicleGroups.Add(group);
            setupContext.Vehicles.Add(vehicle);
            setupContext.Customers.Add(customer);
            await setupContext.SaveChangesAsync();
        }

        await using var firstContext = CreateSharedContext(databaseName);
        await using var secondContext = CreateSharedContext(databaseName);
        var firstRepository = new ReservationRepository(firstContext);
        var secondRepository = new ReservationRepository(secondContext);

        var sharedVehicle = await firstContext.Vehicles.SingleAsync();
        var sharedCustomer = await firstContext.Customers.SingleAsync();
        var overlapStart = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var overlapEnd = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc);

        var beforeParallelSave = await secondRepository.HasOverlappingReservationsAsync(sharedVehicle.Id, overlapStart, overlapEnd);

        firstContext.Reservations.Add(new Reservation
        {
            PublicCode = "PARALLEL-1",
            CustomerId = sharedCustomer.Id,
            VehicleId = sharedVehicle.Id,
            PickupDateTime = overlapStart,
            ReturnDateTime = overlapEnd,
            Status = ReservationStatus.Paid,
            TotalAmount = 1500
        });
        await firstContext.SaveChangesAsync();

        var afterParallelSave = await secondRepository.HasOverlappingReservationsAsync(sharedVehicle.Id, overlapStart.AddHours(2), overlapEnd.AddHours(2));

        beforeParallelSave.Should().BeFalse();
        afterParallelSave.Should().BeTrue();
    }

    [Fact]
    public void ReservationEntity_WhenMapped_ConfiguresVersionAsRowVersionConcurrencyToken()
    {
        using var dbContext = _dbContextFactory.CreateContext();

        var property = dbContext.Model
            .FindEntityType(typeof(Reservation))!
            .FindProperty(nameof(Reservation.Version))!;

        property.IsConcurrencyToken.Should().BeTrue();
        property.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
    }

    private static RentACarDbContext CreateSharedContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<RentACarDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new RentACarDbContext(options);
    }
}
