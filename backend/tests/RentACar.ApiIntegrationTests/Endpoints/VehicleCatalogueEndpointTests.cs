using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class VehicleCatalogueEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupRemoval_PreservesActiveLegacyDepositDependency(bool hasSnapshot)
    {
        var vehicle = await WithDbContextAsync(async db =>
        {
            var car = await db.Vehicles.SingleAsync(v => v.Id == TestDataSeeder.GroupOneId);
            db.Reservations.Add(new Reservation
            {
                PublicCode = "GROUP-DEPENDENCY", VehicleId = car.Id,
                Customer = new Customer { FullName = "Synthetic Group", Email = "group@rentacar.test", Phone = "+900000000000" },
                PickupOfficeId = car.OfficeId, ReturnOfficeId = car.OfficeId,
                PickupDateTime = DateTime.UtcNow.Date.AddDays(10), ReturnDateTime = DateTime.UtcNow.Date.AddDays(13),
                Status = ReservationStatus.Confirmed, TotalAmount = 3000m,
                PricingSnapshot = hasSnapshot ? new ReservationPricingSnapshotV1 { DepositAmount = 2000m, PreAuthorizationAmount = 2000m } : null
            });
            await db.SaveChangesAsync();
            return car;
        });
        var input = new UpdateVehicleRequest(vehicle.Plate, vehicle.Brand, vehicle.Model, vehicle.Year, vehicle.Color,
            null, vehicle.OfficeId, vehicle.Status);
        var endpoint = $"/api/admin/v1/vehicles/{vehicle.Id}";
        using var unauthorized = await Client.PutAsJsonAsync(endpoint, input);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticateAsAdminAsync();
        using var response = await Client.PutAsJsonAsync(endpoint, input);
        response.StatusCode.Should().Be(hasSnapshot ? HttpStatusCode.OK : HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var persisted = await WithDbContextAsync(db => db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id));
        persisted.GroupId.Should().Be(hasSnapshot ? null : vehicle.GroupId);
        var reservation = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        if (hasSnapshot)
        {
            reservation.PricingSnapshot!.DepositAmount.Should().Be(2000m);
        }
        else
        {
            reservation.PricingSnapshot.Should().BeNull();
            persisted.UpdatedAt.Should().Be(vehicle.UpdatedAt);
            await WithDbContextAsync(async db =>
            {
                (await db.Reservations.SingleAsync()).Status = ReservationStatus.Completed;
                await db.SaveChangesAsync();
                return true;
            });
            using var completed = await Client.PutAsJsonAsync(endpoint, input);
            completed.StatusCode.Should().Be(HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
            (await WithDbContextAsync(db => db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id))).GroupId.Should().BeNull();
        }
    }

    [Fact]
    public async Task PhotoUpload_RequiresAdmin_RejectsDisguisedContent_AndServesNewUpload()
    {
        var vehicleId = await WithDbContextAsync(db => db.Vehicles.Select(v => v.Id).FirstAsync());
        var endpoint = $"/api/admin/v1/vehicles/{vehicleId}/photo";
        using var unauthorized = Photo("<svg/>"u8.ToArray());
        (await Client.PostAsync(endpoint, unauthorized)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticateAsAdminAsync();
        using var invalid = Photo("<svg/>"u8.ToArray());
        (await Client.PostAsync(endpoint, invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ1cAAAAASUVORK5CYII=");
        using var valid = Photo(bytes);
        var uploaded = await Client.PostAsync(endpoint, valid);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
        var url = body.RootElement.GetProperty("data").GetProperty("photoUrl").GetString()!;
        try
        {
            Client.DefaultRequestHeaders.Authorization = null;
            var image = await Client.GetAsync(url);
            image.StatusCode.Should().Be(HttpStatusCode.OK);
            image.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            (await image.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
            var publicJson = await Client.GetStringAsync($"/api/v1/vehicles/{vehicleId}");
            publicJson.Should().NotContain("\"plate\"").And.NotContain("\"maintenance\"");
            using var publicBody = JsonDocument.Parse(publicJson);
            publicBody.RootElement.GetProperty("data").GetProperty("dailyPrice").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IVehiclePhotoStorage>().DeleteAsync(url);
        }
    }

    private static MultipartFormDataContent Photo(byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "catalogue.png");
        return form;
    }
}
