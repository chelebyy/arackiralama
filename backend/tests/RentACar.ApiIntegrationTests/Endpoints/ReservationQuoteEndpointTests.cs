using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class ReservationQuoteEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task QuoteAndReservationCreate_PersistSnapshotAndReplayByQuoteId()
    {
        var option = await WithDbContextAsync(async dbContext =>
        {
            var item = await dbContext.ReservationExtraOptions
                .Include(extra => extra.VehicleGroups)
                .OrderBy(extra => extra.SortOrder)
                .FirstAsync();
            item.IsActive = true;
            item.IsArchived = false;
            if (item.VehicleGroups.All(group => group.VehicleGroupId != TestDataSeeder.GroupOneId))
            {
                item.VehicleGroups.Add(new ReservationExtraOptionVehicleGroup
                {
                    OptionId = item.Id,
                    VehicleGroupId = TestDataSeeder.GroupOneId
                });
            }
            await dbContext.SaveChangesAsync();
            return (item.Id, item.Version);
        });
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var sessionId = $"quote-session-{Guid.NewGuid():N}";
        var quoteRequest = new CreateReservationQuoteRequest
        {
            VehicleGroupId = TestDataSeeder.GroupOneId,
            PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            Locale = "tr",
            DriverAge = 30,
            SelectedExtras =
            [
                new SelectedReservationExtraInput
                {
                    OptionId = option.Id,
                    OptionVersion = option.Version,
                    Quantity = 1
                }
            ]
        };
        using var quoteMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing/quote")
        {
            Content = JsonContent.Create(quoteRequest)
        };
        quoteMessage.Headers.Add("X-Session-Id", sessionId);

        var quoteResponse = await Client.SendAsync(quoteMessage);
        var quoteBody = await quoteResponse.Content.ReadAsStringAsync();
        quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK, quoteBody);
        quoteResponse.Headers.CacheControl?.NoStore.Should().BeTrue();
        using var quoteJson = JsonDocument.Parse(quoteBody);
        var quoteData = quoteJson.RootElement.GetProperty("data");
        var quoteId = quoteData.GetProperty("quoteId").GetGuid();
        quoteData.TryGetProperty("priceBreakdown", out _).Should().BeFalse();
        quoteData.GetProperty("extraItems").GetArrayLength().Should().Be(1);

        var reservationRequest = new CreateReservationRequest
        {
            VehicleGroupId = quoteRequest.VehicleGroupId,
            PickupOfficeId = quoteRequest.PickupOfficeId,
            ReturnOfficeId = quoteRequest.ReturnOfficeId,
            PickupDateTimeUtc = quoteRequest.PickupDateTimeUtc,
            ReturnDateTimeUtc = quoteRequest.ReturnDateTimeUtc,
            QuoteId = quoteId,
            Locale = quoteRequest.Locale,
            DriverAge = quoteRequest.DriverAge,
            Customer = new CustomerInfoRequest
            {
                FirstName = "Quote",
                LastName = "Integration",
                Email = $"quote-{Guid.NewGuid():N}@rentacar.test",
                Phone = "+90 555 000 00 09"
            }
        };
        var firstResponse = await SendReservationAsync(reservationRequest, sessionId, $"idem-{Guid.NewGuid():N}");
        var secondResponse = await SendReservationAsync(reservationRequest, sessionId, $"idem-{Guid.NewGuid():N}");
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, firstBody);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, secondBody);
        using var firstJson = JsonDocument.Parse(firstBody);
        using var secondJson = JsonDocument.Parse(secondBody);
        var reservationId = firstJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        secondJson.RootElement.GetProperty("data").GetProperty("id").GetGuid().Should().Be(reservationId);

        var persisted = await WithDbContextAsync(async dbContext =>
            await dbContext.Reservations
                .AsNoTracking()
                .Include(reservation => reservation.SelectedExtras)
                .SingleAsync(reservation => reservation.QuoteId == quoteId));
        persisted.Id.Should().Be(reservationId);
        persisted.PricingSnapshot.Should().NotBeNull();
        persisted.PricingSnapshot!.FinalTotal.Should().Be(persisted.TotalAmount);
        persisted.SelectedExtras.Should().ContainSingle();
    }

    private async Task<HttpResponseMessage> SendReservationAsync(
        CreateReservationRequest request,
        string sessionId,
        string idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservations")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Session-Id", sessionId);
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await Client.SendAsync(message);
    }
}
