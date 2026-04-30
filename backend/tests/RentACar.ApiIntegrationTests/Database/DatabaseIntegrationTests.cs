using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests.Database;

/// <summary>
/// Verifies PostgreSQL-specific database behavior that EF InMemory tests cannot prove.
/// </summary>
public sealed class DatabaseIntegrationTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task Migrations_AreAppliedToFreshPostgresDatabase()
    {
        var pendingMigrations = await WithDbContextAsync(async dbContext =>
            (await dbContext.Database.GetPendingMigrationsAsync()).ToList());

        pendingMigrations.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedData_ContainsBaselineOfficesVehicleGroupsVehiclesAndAdmin()
    {
        var counts = await WithDbContextAsync(async dbContext => new
        {
            Offices = await dbContext.Offices.CountAsync(),
            VehicleGroups = await dbContext.VehicleGroups.CountAsync(),
            Vehicles = await dbContext.Vehicles.CountAsync(),
            AdminUsers = await dbContext.AdminUsers.CountAsync()
        });

        counts.Offices.Should().BeGreaterThanOrEqualTo(2);
        counts.VehicleGroups.Should().BeGreaterThanOrEqualTo(2);
        counts.Vehicles.Should().BeGreaterThanOrEqualTo(4);
        counts.AdminUsers.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task OverlapConstraint_RejectsConflictingActiveReservationsForSameVehicle()
    {
        await WithDbContextAsync(async dbContext =>
        {
            var vehicleId = await dbContext.Vehicles.Select(vehicle => vehicle.Id).FirstAsync();
            var customerId = await CreateCustomerAsync(dbContext, "overlap-a");
            var pickup = DateTime.UtcNow.Date.AddDays(20).AddHours(10);

            dbContext.Reservations.Add(CreateReservation(customerId, vehicleId, pickup, pickup.AddDays(3), ReservationStatus.Hold));
            await dbContext.SaveChangesAsync();

            var secondCustomerId = await CreateCustomerAsync(dbContext, "overlap-b");
            dbContext.Reservations.Add(CreateReservation(secondCustomerId, vehicleId, pickup.AddDays(1), pickup.AddDays(4), ReservationStatus.Hold));

            var act = () => dbContext.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
            return true;
        });
    }

    [Fact]
    public async Task TransactionRollback_RemovesWritesWhenTransactionIsRolledBack()
    {
        var email = $"rollback-{Guid.NewGuid():N}@rentacar.test";

        await WithDbContextAsync(async dbContext =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            dbContext.Customers.Add(new Customer
            {
                Email = email,
                FullName = "Rollback Customer",
                Phone = "+90 555 000 00 04",
                IdentityNumber = "11111111111",
                Nationality = "TR",
                LicenseYear = 2018
            });
            await dbContext.SaveChangesAsync();
            await transaction.RollbackAsync();
            return true;
        });

        var existsAfterRollback = await WithDbContextAsync(dbContext =>
            dbContext.Customers.AnyAsync(customer => customer.Email == email));

        existsAfterRollback.Should().BeFalse();
    }

    [Fact]
    public async Task OptimisticLocking_ThrowsConcurrencyExceptionForStaleReservationUpdate()
    {
        var reservationId = await WithDbContextAsync(async dbContext =>
        {
            var vehicleId = await dbContext.Vehicles.Select(vehicle => vehicle.Id).FirstAsync();
            var customerId = await CreateCustomerAsync(dbContext, "concurrency");
            var pickup = DateTime.UtcNow.Date.AddDays(30).AddHours(10);
            var reservation = CreateReservation(customerId, vehicleId, pickup, pickup.AddDays(2), ReservationStatus.Draft);
            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync();
            return reservation.Id;
        });

        using var firstScope = Services.CreateScope();
        using var secondScope = Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var firstReservation = await firstContext.Reservations.FirstAsync(reservation => reservation.Id == reservationId);
        var secondReservation = await secondContext.Reservations.FirstAsync(reservation => reservation.Id == reservationId);

        firstReservation.Status = ReservationStatus.Hold;
        await firstContext.SaveChangesAsync();

        secondReservation.Status = ReservationStatus.Cancelled;
        var act = () => secondContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static async Task<Guid> CreateCustomerAsync(RentACarDbContext dbContext, string prefix)
    {
        var customer = new Customer
        {
            Email = $"{prefix}-{Guid.NewGuid():N}@rentacar.test",
            FullName = "Integration Customer",
            Phone = "+90 555 000 00 05",
            IdentityNumber = Guid.NewGuid().ToString("N")[..11],
            Nationality = "TR",
            LicenseYear = 2018
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        return customer.Id;
    }

    private static Reservation CreateReservation(
        Guid customerId,
        Guid vehicleId,
        DateTime pickup,
        DateTime dropoff,
        ReservationStatus status) => new()
    {
        PublicCode = $"IT{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
        CustomerId = customerId,
        VehicleId = vehicleId,
        PickupDateTime = pickup,
        ReturnDateTime = dropoff,
        Status = status,
        TotalAmount = 1000m
    };
}
