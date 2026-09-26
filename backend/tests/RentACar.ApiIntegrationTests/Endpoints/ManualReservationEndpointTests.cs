using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Reservations;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class ManualReservationEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, false, 750)]
    [InlineData(false, true, 0)]
    [InlineData(false, true, 750)]
    [InlineData(true, false, 0)]
    [InlineData(true, false, 750)]
    [InlineData(true, true, 0)]
    [InlineData(true, true, 750)]
    public async Task ManualReservation_PreservesAcceptedDepositAndRepricesVehicleTerms(
        bool groupless, bool overrideTotal, int deposit)
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == TestDataSeeder.GroupOneId);
            if (groupless) vehicle.GroupId = null;
            vehicle.RentalTerms!.DepositAmount = deposit;
            foreach (var rate in vehicle.RentalTerms.Rates) rate.DailyPrice = 1200m;
            return await db.SaveChangesAsync();
        });
        await AuthenticateAsAdminAsync();
        using var created = await Client.PostAsJsonAsync("/api/admin/v1/reservations/manual", new AdminManualReservationRequest
        {
            VehicleId = TestDataSeeder.GroupOneId, PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId, PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3), CustomerFirstName = "Synthetic", CustomerLastName = "Manual",
            CustomerPhone = "+900000000000", CustomerEmail = "manual@rentacar.test",
            TotalAmount = overrideTotal ? 2750m : null
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        var id = data.GetProperty("id").GetGuid();
        data.GetProperty("depositAmount").GetDecimal().Should().Be(deposit);
        data.GetProperty("totalAmount").GetDecimal().Should().Be(overrideTotal ? 2750m : 3600m);
        await WithDbContextAsync(async db =>
        {
            var vehicle = await db.Vehicles.SingleAsync(v => v.Id == TestDataSeeder.GroupOneId);
            vehicle.RentalTerms!.DepositAmount = 9999m;
            foreach (var rate in vehicle.RentalTerms.Rates) rate.DailyPrice = 1300m;
            return await db.SaveChangesAsync();
        });
        using var notes = await Client.PutAsJsonAsync($"/api/admin/v1/reservations/{id}",
            new UpdateReservationRequest { Notes = "Only notes changed" });
        notes.StatusCode.Should().Be(HttpStatusCode.OK, await notes.Content.ReadAsStringAsync());
        var unchanged = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        unchanged.TotalAmount.Should().Be(overrideTotal ? 2750m : 3600m);
        unchanged.PricingSnapshot!.DepositAmount.Should().Be(deposit);
        unchanged.PricingSnapshot.FinalTotal.Should().Be(unchanged.TotalAmount);
        unchanged.Notes.Should().Be("Only notes changed");

        using var updated = await Client.PutAsJsonAsync($"/api/admin/v1/reservations/{id}",
            new UpdateReservationRequest { ReturnDateTimeUtc = pickup.AddDays(4) });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var reservation = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id));
        reservation.TotalAmount.Should().Be(5200m);
        reservation.PricingSnapshot!.FinalTotal.Should().Be(5200m);
        reservation.PricingSnapshot.RentalDays.Should().Be(4);
        reservation.PricingSnapshot.DepositAmount.Should().Be(deposit);
        reservation.PricingSnapshot.PreAuthorizationAmount.Should().Be(deposit);
        reservation.PricingSnapshot.BookingConditions.Should().BeNull();
        reservation.QuoteId.Should().BeNull();
        reservation.OccupiedUntilUtc.Should().Be(pickup.AddDays(4).AddHours(1));
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(0, HttpStatusCode.OK)]
    public async Task AssignVehicle_ChecksPreparationTail(int nextPickupOffset, HttpStatusCode expected)
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var until = pickup.AddDays(3);
        var ids = await WithDbContextAsync(async db =>
        {
            var target = new Vehicle { Plate = "REASSIGN-TARGET", Brand = "Synthetic", Model = "Target", Year = 2026,
                OfficeId = TestDataSeeder.OfficeOneId, GroupId = TestDataSeeder.GroupOneId };
            db.Vehicles.Add(target);
            var customer = new Customer { FullName = "Synthetic Assign", Email = "assign@rentacar.test", Phone = "+900000000000" };
            var reservation = new Reservation
            {
                PublicCode = "REASSIGN-ORIGINAL", Customer = customer, VehicleId = TestDataSeeder.GroupOneId,
                PickupOfficeId = TestDataSeeder.OfficeOneId, ReturnOfficeId = TestDataSeeder.OfficeOneId,
                PickupDateTime = pickup, ReturnDateTime = until, OccupiedUntilUtc = until.AddHours(1),
                Status = ReservationStatus.Confirmed, TotalAmount = 3000m
            };
            db.Reservations.Add(reservation);
            db.Reservations.Add(new Reservation
            {
                PublicCode = "REASSIGN-NEXT", Customer = customer, Vehicle = target,
                PickupOfficeId = TestDataSeeder.OfficeOneId, ReturnOfficeId = TestDataSeeder.OfficeOneId,
                PickupDateTime = until.AddMinutes(60 + nextPickupOffset), ReturnDateTime = until.AddDays(2),
                Status = ReservationStatus.Confirmed, TotalAmount = 2000m
            });
            await db.SaveChangesAsync();
            return (Reservation: reservation.Id, Target: target.Id);
        });
        await AuthenticateAsAdminAsync();
        using var response = await Client.PostAsJsonAsync($"/api/admin/v1/reservations/{ids.Reservation}/assign-vehicle", ids.Target);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        if (expected == HttpStatusCode.BadRequest) body.Should().Contain("overlapping reservations");
        var persisted = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == ids.Reservation));
        persisted.VehicleId.Should().Be(expected == HttpStatusCode.OK ? ids.Target : TestDataSeeder.GroupOneId);
        persisted.OccupiedUntilUtc.Should().Be(until.AddHours(1));
    }
}
