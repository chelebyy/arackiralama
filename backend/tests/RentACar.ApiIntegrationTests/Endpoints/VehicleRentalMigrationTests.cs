using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class VehicleRentalMigrationTests(RedisFixture redis) : ApiIntegrationTestBase(redis)
{
    [Fact]
    public async Task Backfill_CopiesGroupRulesAndLeavesHistoricalContractUnchanged()
    {
        await WithDbContextAsync(async db =>
        {
            var customer = new Customer { FullName = "Synthetic Migration", Email = "migration@example.test", Phone = "+900000000000" };
            var snapshot = new ReservationPricingSnapshotV1 { DailyRate = 700, RentalDays = 2, BaseTotal = 1400, FinalTotal = 1400, DepositAmount = 800 };
            var reservation = new Reservation
            {
                PublicCode = "MIGRATION-HISTORY", Customer = customer, VehicleId = TestDataSeeder.GroupOneId,
                PickupOfficeId = TestDataSeeder.OfficeOneId, ReturnOfficeId = TestDataSeeder.OfficeOneId,
                PickupDateTime = DateTime.UtcNow.AddDays(-20), ReturnDateTime = DateTime.UtcNow.AddDays(-18),
                Status = ReservationStatus.Completed, TotalAmount = 1400, PricingSnapshot = snapshot
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
            var original = JsonSerializer.Serialize(snapshot);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260924181928_VehicleCatalogue");
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var vehicle = await db.Vehicles.Include(v => v.Group).SingleAsync(v => v.Id == TestDataSeeder.GroupOneId);
            var rates = await db.PricingRules.Where(r => r.VehicleGroupId == vehicle.GroupId).ToListAsync();
            vehicle.RentalTerms!.DepositAmount.Should().Be(vehicle.Group!.DepositAmount);
            vehicle.RentalTerms.MinAge.Should().Be(vehicle.Group.MinAge);
            vehicle.RentalTerms.MinLicenseYears.Should().Be(vehicle.Group.MinLicenseYears);
            vehicle.RentalTerms.Rates.Select(r => (r.Id, r.DailyPrice, r.StartDate, r.EndDate, r.Priority, r.Multiplier, r.WeekdayMultiplier, r.WeekendMultiplier, r.CalculationType))
                .Should().BeEquivalentTo(rates.Select(r => (r.Id, r.DailyPrice, r.StartDate, r.EndDate, r.Priority, r.Multiplier, r.WeekdayMultiplier, r.WeekendMultiplier, r.CalculationType)));
            var history = await db.Reservations.SingleAsync(r => r.Id == reservation.Id);
            JsonSerializer.Serialize(history.PricingSnapshot).Should().Be(original);
            history.TotalAmount.Should().Be(1400);
            history.Status.Should().Be(ReservationStatus.Completed);
            history.OccupiedUntilUtc.Should().BeNull();
            (await db.Offices.SingleAsync(o => o.Id == TestDataSeeder.OfficeOneId)).OperatingPolicy.Should().BeNull();
            return true;
        });
    }
}
