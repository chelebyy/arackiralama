using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class VehicleCatalogueEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
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
