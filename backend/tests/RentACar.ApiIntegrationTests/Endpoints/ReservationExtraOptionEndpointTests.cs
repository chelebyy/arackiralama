using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class ReservationExtraOptionEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task AdminCatalog_RejectsUnauthenticatedRequest()
    {
        var response = await Client.GetAsync("/api/admin/v1/reservation-extra-options");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminCatalog_RejectsCustomerRole()
    {
        var token = await TestJwtFactory.CreateCustomerTokenAsync(Services);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/admin/v1/reservation-extra-options");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task AdminCatalog_AllowsAdminRoles(string role)
    {
        var token = await TestJwtFactory.CreateAdminTokenAsync(Services, role);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/admin/v1/reservation-extra-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task PublicCatalog_ReturnsLocalizedGroupItemsAndNoStore()
    {
        var assignedGroupId = await WithDbContextAsync(dbContext =>
            dbContext.ReservationExtraOptionVehicleGroups
                .Select(assignment => assignment.VehicleGroupId)
                .FirstAsync());
        var response = await Client.GetAsync(
            $"/api/v1/reservation-extra-options?vehicleGroupId={assignedGroupId}&locale=tr");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("PER_DAY");
        body.ToLowerInvariant().Should().NotContain("translations");
    }

    [Fact]
    public async Task PublicCatalog_RejectsUnsupportedLocale()
    {
        var response = await Client.GetAsync(
            $"/api/v1/reservation-extra-options?vehicleGroupId={TestDataSeeder.GroupOneId}&locale=xx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
