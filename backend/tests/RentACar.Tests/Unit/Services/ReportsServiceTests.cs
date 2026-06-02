using FluentAssertions;
using RentACar.API.Contracts.Reports;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class ReportsServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACarDbContext _dbContext;
    private readonly ReportsService _sut;

    public ReportsServiceTests()
    {
        _dbContext = _dbFactory.CreateContext();
        _sut = new ReportsService(_dbContext);
    }

    [Fact]
    public async Task GetRevenueReportAsync_WithInvalidPeriod_ReturnsEmpty()
    {
        var result = await _sut.GetRevenueReportAsync("bogus", CancellationToken.None);

        result.Period.Should().Be("bogus");
        result.TotalRevenue.Should().Be(0m);
        result.TotalReservations.Should().Be(0);
        result.AverageOrderValue.Should().Be(0m);
        result.DailyBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRevenueReportAsync_WithEmptyPeriod_ReturnsEmpty()
    {
        var result = await _sut.GetRevenueReportAsync(string.Empty, CancellationToken.None);

        result.TotalRevenue.Should().Be(0m);
        result.DailyBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRevenueReportAsync_WithValidPeriod_AggregatesByDay()
    {
        var today = DateTime.UtcNow.Date;
        var reservationId1 = Guid.NewGuid();
        var reservationId2 = Guid.NewGuid();
        SeedPaymentIntentFor(reservationId1, today.AddHours(10), PaymentStatus.Succeeded, 100m);
        SeedPaymentIntentFor(reservationId2, today.AddHours(14), PaymentStatus.Succeeded, 250m);
        SeedReservationWith(today.AddHours(9), ReservationStatus.Paid, reservationId1);
        SeedReservationWith(today.AddHours(15), ReservationStatus.Active, reservationId2);
        SeedReservationWith(today.AddHours(18), ReservationStatus.Cancelled, Guid.NewGuid());

        var result = await _sut.GetRevenueReportAsync("daily", CancellationToken.None);

        result.Period.Should().Be("daily");
        result.TotalRevenue.Should().Be(350m);
        result.TotalReservations.Should().Be(2);
        result.AverageOrderValue.Should().Be(175m);
        result.DailyBreakdown.Should().HaveCount(1);
        result.DailyBreakdown[0].Revenue.Should().Be(350m);
        result.DailyBreakdown[0].Reservations.Should().Be(2);
    }

    [Fact]
    public async Task GetRevenueReportAsync_UsesEligibleReservationScopeForRevenue()
    {
        var today = DateTime.UtcNow.Date;
        var scopedReservationId = Guid.NewGuid();
        var cancelledReservationId = Guid.NewGuid();

        SeedReservationWith(today.AddHours(9), ReservationStatus.Completed, scopedReservationId);
        SeedReservationWith(today.AddHours(11), ReservationStatus.Cancelled, cancelledReservationId);
        SeedPaymentIntentFor(scopedReservationId, today.AddDays(-5), PaymentStatus.Succeeded, 400m);
        SeedPaymentIntentFor(cancelledReservationId, today.AddHours(12), PaymentStatus.Succeeded, 900m);

        var result = await _sut.GetRevenueReportAsync("daily", CancellationToken.None);

        result.TotalRevenue.Should().Be(400m);
        result.TotalReservations.Should().Be(1);
        result.AverageOrderValue.Should().Be(400m);
        result.DailyBreakdown[0].Revenue.Should().Be(400m);
        result.DailyBreakdown[0].Reservations.Should().Be(1);
    }

    [Fact]
    public async Task GetRevenueReportAsync_WhenNoEligibleReservations_IgnoresSucceededPayments()
    {
        var today = DateTime.UtcNow.Date;
        SeedPaymentIntent(today.AddHours(10), PaymentStatus.Succeeded, 500m);

        var result = await _sut.GetRevenueReportAsync("daily", CancellationToken.None);

        result.TotalRevenue.Should().Be(0m);
        result.TotalReservations.Should().Be(0);
        result.DailyBreakdown[0].Revenue.Should().Be(0m);
    }

    [Fact]
    public async Task GetRevenueReportAsync_WithWeeklyPeriod_ReturnsSevenDays()
    {
        var result = await _sut.GetRevenueReportAsync("weekly", CancellationToken.None);

        result.DailyBreakdown.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetRevenueReportAsync_WithMonthlyPeriod_ReturnsThirtyDays()
    {
        var result = await _sut.GetRevenueReportAsync("monthly", CancellationToken.None);

        result.DailyBreakdown.Should().HaveCount(30);
    }

    [Fact]
    public async Task GetOccupancyReportAsync_WithInvalidPeriod_ReturnsEmpty()
    {
        var result = await _sut.GetOccupancyReportAsync("bogus", CancellationToken.None);

        result.Period.Should().Be("bogus");
        result.TotalVehicles.Should().Be(0);
        result.DailyBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOccupancyReportAsync_CalculatesOccupancyRate()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        SeedVehicle();
        SeedVehicle();
        SeedVehicle();
        SeedVehicle();

        SeedReservation(today.AddHours(8), tomorrow.AddHours(8), ReservationStatus.Active);
        SeedReservation(today.AddHours(10), tomorrow.AddHours(10), ReservationStatus.Completed);

        var result = await _sut.GetOccupancyReportAsync("daily", CancellationToken.None);

        result.TotalVehicles.Should().Be(4);
        result.DailyBreakdown.Should().HaveCount(1);
        var bucket = result.DailyBreakdown[0];
        bucket.TotalVehicles.Should().Be(4);
        bucket.OccupiedVehicles.Should().Be(2);
        bucket.OccupancyRate.Should().Be(50m);
        result.OccupancyRate.Should().Be(50m);
    }

    [Fact]
    public async Task GetOccupancyReportAsync_FiltersReservationsOutsideRequestedPeriod()
    {
        var today = DateTime.UtcNow.Date;
        var groupId = Guid.NewGuid();

        SeedVehicle("Renault", "Clio", groupId);
        SeedVehicle("Ford", "Focus", groupId);

        SeedReservation(today.AddHours(8), today.AddDays(1).AddHours(8), ReservationStatus.Active);
        SeedReservation(today.AddDays(-20), today.AddDays(-19), ReservationStatus.Completed);

        var result = await _sut.GetOccupancyReportAsync("daily", CancellationToken.None);

        result.TotalVehicles.Should().Be(2);
        result.OccupiedVehicles.Should().Be(1);
        result.OccupancyRate.Should().Be(50m);
        result.DailyBreakdown[0].OccupiedVehicles.Should().Be(1);
        result.DailyBreakdown[0].OccupancyRate.Should().Be(50m);
    }

    [Fact]
    public async Task GetOccupancyReportAsync_ExcludesCancelledReservations()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        SeedVehicle();
        SeedReservation(today.AddHours(8), tomorrow.AddHours(8), ReservationStatus.Cancelled);

        var result = await _sut.GetOccupancyReportAsync("daily", CancellationToken.None);

        result.TotalVehicles.Should().Be(1);
        result.DailyBreakdown[0].OccupiedVehicles.Should().Be(0);
        result.DailyBreakdown[0].OccupancyRate.Should().Be(0m);
    }

    [Fact]
    public async Task GetOccupancyReportAsync_WithNoVehicles_ReturnsZeroRate()
    {
        var result = await _sut.GetOccupancyReportAsync("daily", CancellationToken.None);

        result.TotalVehicles.Should().Be(0);
        result.OccupancyRate.Should().Be(0m);
        result.DailyBreakdown[0].OccupancyRate.Should().Be(0m);
    }

    [Fact]
    public async Task GetPopularVehiclesAsync_WithInvalidPeriod_ReturnsEmpty()
    {
        var result = await _sut.GetPopularVehiclesAsync("bogus", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPopularVehiclesAsync_RanksByRentalCountDescending()
    {
        var today = DateTime.UtcNow.Date;
        var groupId = Guid.NewGuid();

        var v1 = SeedVehicle("Renault", "Clio", groupId);
        var v2 = SeedVehicle("Ford", "Focus", groupId);
        var v3 = SeedVehicle("Volkswagen", "Polo", groupId);

        SeedReservation(today.AddHours(8), today.AddDays(1).AddHours(8), ReservationStatus.Paid, v1);
        SeedReservation(today.AddHours(9), today.AddDays(1).AddHours(9), ReservationStatus.Paid, v1);
        SeedReservation(today.AddHours(10), today.AddDays(1).AddHours(10), ReservationStatus.Paid, v1);
        SeedReservation(today.AddHours(11), today.AddDays(1).AddHours(11), ReservationStatus.Completed, v2);
        SeedReservation(today.AddHours(12), today.AddDays(1).AddHours(12), ReservationStatus.Completed, v2);

        var result = await _sut.GetPopularVehiclesAsync("yearly", CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].VehicleName.Should().Be("Renault Clio");
        result[0].RentalCount.Should().Be(3);
        result[1].VehicleName.Should().Be("Ford Focus");
        result[1].RentalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPopularVehiclesAsync_AggregatesRevenueFromSucceededPayments()
    {
        var today = DateTime.UtcNow.Date;
        var vehicleId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        SeedVehicle("BMW", "320i", groupId, vehicleId);

        var reservationId = Guid.NewGuid();
        _dbContext.Reservations.Add(new Reservation
        {
            Id = reservationId,
            PublicCode = "R-1",
            CustomerId = Guid.NewGuid(),
            VehicleId = vehicleId,
            PickupDateTime = today.AddHours(8),
            ReturnDateTime = today.AddDays(1).AddHours(8),
            Status = ReservationStatus.Completed,
            TotalAmount = 1000m
        });
        _dbContext.PaymentIntents.Add(new PaymentIntent
        {
            ReservationId = reservationId,
            Amount = 1000m,
            Status = PaymentStatus.Succeeded,
            Provider = "Mock",
            IdempotencyKey = Guid.NewGuid().ToString()
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetPopularVehiclesAsync("yearly", CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].VehicleName.Should().Be("BMW 320i");
        result[0].RentalCount.Should().Be(1);
        result[0].Revenue.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPopularVehiclesAsync_UsesReservationScopeForRevenueRegardlessOfPaymentDate()
    {
        var today = DateTime.UtcNow.Date;
        var vehicleId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        SeedVehicle("Toyota", "Corolla", groupId, vehicleId);

        var scopedReservationId = Guid.NewGuid();
        _dbContext.Reservations.Add(new Reservation
        {
            Id = scopedReservationId,
            PublicCode = "R-SCOPED",
            CustomerId = Guid.NewGuid(),
            VehicleId = vehicleId,
            PickupDateTime = today.AddHours(8),
            ReturnDateTime = today.AddDays(1).AddHours(8),
            Status = ReservationStatus.Completed,
            TotalAmount = 750m
        });
        _dbContext.PaymentIntents.Add(new PaymentIntent
        {
            ReservationId = scopedReservationId,
            Amount = 750m,
            Status = PaymentStatus.Succeeded,
            Provider = "Mock",
            IdempotencyKey = Guid.NewGuid().ToString(),
            CreatedAt = today.AddDays(-10)
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetPopularVehiclesAsync("daily", CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].RentalCount.Should().Be(1);
        result[0].Revenue.Should().Be(750m);
    }

    [Fact]
    public async Task GetPopularVehiclesAsync_WhenNoReservations_ReturnsEmpty()
    {
        var result = await _sut.GetPopularVehiclesAsync("yearly", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPopularVehiclesAsync_CapsAtTopFive()
    {
        var today = DateTime.UtcNow.Date;
        var groupId = Guid.NewGuid();
        for (var i = 0; i < 7; i++)
        {
            var vehicleId = Guid.NewGuid();
            SeedVehicle("Brand", $"M{i}", groupId, vehicleId);
            for (var j = 0; j <= i; j++)
            {
                SeedReservation(
                    today.AddHours(8 + j),
                    today.AddDays(1).AddHours(8 + j),
                    ReservationStatus.Paid,
                    vehicleId);
            }
        }

        var result = await _sut.GetPopularVehiclesAsync("yearly", CancellationToken.None);

        result.Should().HaveCount(5);
        result[0].RentalCount.Should().Be(7);
        result[4].RentalCount.Should().Be(3);
    }

    private void SeedPaymentIntent(DateTime createdAt, PaymentStatus status, decimal amount)
    {
        SeedPaymentIntentFor(Guid.NewGuid(), createdAt, status, amount);
    }

    private void SeedPaymentIntentFor(Guid reservationId, DateTime createdAt, PaymentStatus status, decimal amount)
    {
        _dbContext.PaymentIntents.Add(new PaymentIntent
        {
            ReservationId = reservationId,
            Amount = amount,
            Status = status,
            Provider = "Mock",
            IdempotencyKey = Guid.NewGuid().ToString(),
            CreatedAt = createdAt
        });
        _dbContext.SaveChanges();
    }

    private void SeedReservation(DateTime pickup, ReservationStatus status)
    {
        SeedReservationWith(pickup, status, Guid.NewGuid());
    }

    private void SeedReservationWith(DateTime pickup, ReservationStatus status, Guid reservationId)
    {
        _dbContext.Reservations.Add(new Reservation
        {
            Id = reservationId,
            PublicCode = $"R-{Guid.NewGuid():N}",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = pickup,
            ReturnDateTime = pickup.AddDays(1),
            Status = status,
            TotalAmount = 100m
        });
        _dbContext.SaveChanges();
    }

    private void SeedReservation(DateTime pickup, DateTime ret, ReservationStatus status, Guid? vehicleId = null)
    {
        _dbContext.Reservations.Add(new Reservation
        {
            PublicCode = $"R-{Guid.NewGuid():N}",
            CustomerId = Guid.NewGuid(),
            VehicleId = vehicleId ?? Guid.NewGuid(),
            PickupDateTime = pickup,
            ReturnDateTime = ret,
            Status = status,
            TotalAmount = 100m
        });
        _dbContext.SaveChanges();
    }

    private Guid SeedVehicle(string brand = "Renault", string model = "Clio", Guid? groupId = null, Guid? id = null)
    {
        var vehicle = new Vehicle
        {
            Id = id ?? Guid.NewGuid(),
            Plate = $"07ABC{Guid.NewGuid().ToString("N")[..4]}",
            Brand = brand,
            Model = model,
            Year = 2024,
            Color = "White",
            GroupId = groupId ?? Guid.NewGuid(),
            OfficeId = Guid.NewGuid(),
            Status = VehicleStatus.Available
        };
        _dbContext.Vehicles.Add(vehicle);
        _dbContext.SaveChanges();
        return vehicle.Id;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }
}
