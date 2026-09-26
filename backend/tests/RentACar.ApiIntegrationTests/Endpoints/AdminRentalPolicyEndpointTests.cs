using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class AdminRentalPolicyEndpointTests(RedisFixture redis) : ApiIntegrationTestBase(redis)
{
    [Fact]
    public async Task Admin_CanCreateGrouplessVehicleWithOwnedRates()
    {
        await AuthenticateAsAdminAsync();
        var terms = new VehicleRentalTerms
        {
            DepositAmount = 2500, MinAge = 23, MinLicenseYears = 3,
            Rates = [new() { StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), DailyPrice = 1750 }]
        };
        using var response = await Client.PostAsJsonAsync("/api/admin/v1/vehicles", new
        {
            Plate = "IT-PKG2-NEW", Brand = "Synthetic", Model = "Independent", Year = 2026, Color = "White",
            GroupId = (Guid?)null, OfficeId = TestDataSeeder.OfficeOneId, Status = 0, RentalTerms = terms
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var vehicle = await WithDbContextAsync(db => db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == id));
        vehicle.GroupId.Should().BeNull();
        vehicle.RentalTerms!.Rates.Single().DailyPrice.Should().Be(1750);
        Client.DefaultRequestHeaders.Authorization = null;
        using var publicResponse = await Client.GetAsync($"/api/v1/vehicles/{id}");
        var publicBody = await publicResponse.Content.ReadAsStringAsync();
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        publicBody.Should().NotContain("IT-PKG2-NEW").And.NotContain("rentalTerms");
        using var publicJson = JsonDocument.Parse(publicBody);
        publicJson.RootElement.GetProperty("data").GetProperty("minAge").GetInt32().Should().Be(23);
    }

    [Fact]
    public async Task OfficePolicy_RequiresAdminAndRejectsIncompleteConfiguration()
    {
        var payload = new
        {
            Name = "Synthetic Policy Office", Code = "it-policy", Address = "Synthetic address", Phone = "+900000000000",
            IsActive = true, IsAirport = false, OpeningHours = "Configured separately",
            OperatingPolicy = new OfficeOperatingPolicy { MinimumNoticeMinutes = null, PreparationMinutes = 60 }
        };
        using var anonymous = await Client.PutAsJsonAsync($"/api/admin/v1/offices/{TestDataSeeder.OfficeOneId}", payload);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticateAsAdminAsync();
        using var invalid = await Client.PutAsJsonAsync($"/api/admin/v1/offices/{TestDataSeeder.OfficeOneId}", payload);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest, await invalid.Content.ReadAsStringAsync());
        payload.OperatingPolicy.MinimumNoticeMinutes = 120;
        payload.OperatingPolicy.PickupWindows = [new() { Day = DayOfWeek.Monday, StartMinute = 540, EndMinute = 1080 }];
        payload.OperatingPolicy.ReturnWindows = [new() { Day = DayOfWeek.Monday, StartMinute = 540, EndMinute = 1080 }];
        using var valid = await Client.PutAsJsonAsync($"/api/admin/v1/offices/{TestDataSeeder.OfficeOneId}", payload);
        valid.StatusCode.Should().Be(HttpStatusCode.OK, await valid.Content.ReadAsStringAsync());
        var stored = await WithDbContextAsync(db => db.Offices.AsNoTracking().SingleAsync(o => o.Id == TestDataSeeder.OfficeOneId));
        stored.OperatingPolicy!.MinimumNoticeMinutes.Should().Be(120);
        stored.OperatingPolicy.PickupWindows.Single().StartMinute.Should().Be(540);
    }
}
