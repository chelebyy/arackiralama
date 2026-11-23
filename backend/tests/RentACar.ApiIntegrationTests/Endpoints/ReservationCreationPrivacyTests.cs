using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Contracts.Reservations;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using StackExchange.Redis;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class ReservationCreationPrivacyTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Create_WithExistingCustomerEmail_DoesNotExposeProfileOrHistory(bool exact, bool unpaid)
    {
        var victim = new Customer
        {
            FullName = "Existing private profile",
            Email = $"existing-{Guid.NewGuid():N}@example.test",
            Phone = "+905550000001",
            IdentityNumber = "PRIVATE-IDENTITY"
        };
        await WithDbContextAsync(async db =>
        {
            db.Customers.Add(victim);
            db.Reservations.Add(new Reservation
            {
                PublicCode = "PAST-PRIVATE",
                CustomerId = victim.Id,
                VehicleId = TestDataSeeder.GroupTwoId,
                PickupOfficeId = TestDataSeeder.OfficeOneId,
                ReturnOfficeId = TestDataSeeder.OfficeOneId,
                PickupDateTime = DateTime.UtcNow.AddDays(-10),
                ReturnDateTime = DateTime.UtcNow.AddDays(-7),
                Status = ReservationStatus.Completed,
                TotalAmount = 12345m
            });
            return await db.SaveChangesAsync();
        });
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var session = Guid.NewGuid().ToString();
        Guid? quoteId = null;
        if (exact)
        {
            using var quoteMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing/quote")
            {
                Content = JsonContent.Create(new CreateReservationQuoteRequest
                {
                    VehicleId = TestDataSeeder.GroupOneId,
                    VehicleGroupId = TestDataSeeder.GroupOneId,
                    PickupOfficeId = TestDataSeeder.OfficeOneId,
                    ReturnOfficeId = TestDataSeeder.OfficeOneId,
                    PickupDateTimeUtc = pickup,
                    ReturnDateTimeUtc = pickup.AddDays(3),
                    DriverAge = 30
                })
            };
            quoteMessage.Headers.Add("X-Session-Id", session);
            using var quoteResponse = await Client.SendAsync(quoteMessage);
            var quoteBody = await quoteResponse.Content.ReadAsStringAsync();
            quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK, quoteBody);
            using var quoteJson = JsonDocument.Parse(quoteBody);
            quoteId = quoteJson.RootElement.GetProperty("data").GetProperty("quoteId").GetGuid();
        }
        var request = new CreateReservationRequest
        {
            VehicleId = exact ? TestDataSeeder.GroupOneId : null,
            VehicleGroupId = TestDataSeeder.GroupOneId,
            PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            QuoteId = quoteId,
            DriverAge = 30,
            Driver = new DriverInfoRequest { LicenseExpiryDate = pickup.AddYears(2) },
            Customer = new CustomerInfoRequest
            {
                FirstName = "Unverified",
                LastName = "Caller",
                Email = victim.Email,
                Phone = "+905550000002",
                DateOfBirth = pickup.AddYears(-30),
                DriverLicenseIssueDate = pickup.AddYears(-5)
            }
        };
        var endpoint = unpaid ? "/api/v1/reservations/unpaid-requests" : "/api/v1/reservations";
        var idempotencyKey = Guid.NewGuid().ToString();
        if (!exact)
        {
            var previousResponse = JsonSerializer.Serialize(new
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new { data = new { customerPhone = victim.Phone } })
            });
            var redis = Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            await redis.StringSetAsync($"idempotent:public-create-v1:{idempotencyKey}", previousResponse, TimeSpan.FromMinutes(5));
        }
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Session-Id", session);
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await Client.SendAsync(message);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        data.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["id", "publicCode", "status"]);
        body.Should().NotContain(victim.FullName).And.NotContain(victim.Phone).And.NotContain(victim.IdentityNumber);
        var id = data.GetProperty("id").GetGuid();
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id));
        saved.CustomerId.Should().Be(victim.Id);
        saved.PublicCode.Should().Be(data.GetProperty("publicCode").GetString());
        (await WithDbContextAsync(db => db.Customers.CountAsync(item => item.NormalizedEmail == victim.NormalizedEmail)))
            .Should().Be(1);

        using var replayMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request)
        };
        replayMessage.Headers.Add("X-Session-Id", session);
        replayMessage.Headers.Add("Idempotency-Key", idempotencyKey);
        using var replay = await Client.SendAsync(replayMessage);
        var replayBody = await replay.Content.ReadAsStringAsync();
        replay.StatusCode.Should().Be(HttpStatusCode.OK, replayBody);
        using var replayJson = JsonDocument.Parse(replayBody);
        replayJson.RootElement.GetProperty("data").GetRawText().Should().Be(data.GetRawText());
        if (!exact)
        {
            replay.Headers.GetValues("X-Idempotency-Status").Should().Contain("HIT");
        }
        (await WithDbContextAsync(db => db.Reservations.CountAsync(item => item.CustomerId == victim.Id)))
            .Should().Be(2);
    }
}
