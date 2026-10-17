using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Reservations;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class IdempotencyEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task CachedReservationRetries_RemainRateLimitedAndCreateOnlyOneReservation()
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var sessionId = $"replay-session-{Guid.NewGuid():N}";
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var request = new CreateReservationRequest
        {
            VehicleGroupId = TestDataSeeder.GroupOneId,
            PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            Customer = new CustomerInfoRequest
            {
                FirstName = "Synthetic",
                LastName = "Replay",
                Email = $"replay-{Guid.NewGuid():N}@example.test",
                Phone = "+900000000000"
            }
        };
        string? firstBody = null;
        for (var index = 0; index < 6; index++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservations")
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Add("Idempotency-Key", idempotencyKey);
            message.Headers.Add("X-Session-Id", sessionId);
            using var response = await Client.SendAsync(message);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(index < 5 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, body);
            if (index == 0)
            {
                firstBody = body;
            }
            else if (index < 5)
            {
                response.Headers.GetValues("X-Idempotency-Status").Should().Contain("HIT");
                body.Should().Be(firstBody);
            }
        }

        var reservations = await WithDbContextAsync(dbContext => dbContext.Reservations.CountAsync());
        reservations.Should().Be(1);
    }
}
