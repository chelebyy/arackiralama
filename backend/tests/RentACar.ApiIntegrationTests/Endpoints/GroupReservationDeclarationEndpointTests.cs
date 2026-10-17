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

public sealed class GroupReservationDeclarationEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Theory]
    [InlineData("quoted-age", false, HttpStatusCode.Conflict)]
    [InlineData("quoted-age", true, HttpStatusCode.Conflict)]
    [InlineData("minimum-age", false, HttpStatusCode.Conflict)]
    [InlineData("minimum-age", true, HttpStatusCode.Conflict)]
    [InlineData("licence-years", false, HttpStatusCode.Conflict)]
    [InlineData("licence-years", true, HttpStatusCode.Conflict)]
    [InlineData("licence-validity", false, HttpStatusCode.BadRequest)]
    [InlineData("licence-validity", true, HttpStatusCode.BadRequest)]
    [InlineData("documents", false, HttpStatusCode.BadRequest)]
    [InlineData("documents", true, HttpStatusCode.BadRequest)]
    [InlineData("missing", false, HttpStatusCode.BadRequest)]
    [InlineData("missing", true, HttpStatusCode.BadRequest)]
    public async Task GroupQuoteCreate_RejectsInvalidDeclarationAndAllowsCorrectedRetry(
        string invalid, bool unpaid, HttpStatusCode expected)
    {
        var age = invalid == "minimum-age" ? 20 : 30;
        var session = Guid.NewGuid().ToString("N");
        var request = await CreateQuotedRequestAsync(age, session);
        var declaration = invalid switch
        {
            "quoted-age" => ValidDeclaration(22),
            "minimum-age" => ValidDeclaration(20),
            "licence-years" => ValidDeclaration(30) with { LicenseYearsAtPickup = 1 },
            "licence-validity" => ValidDeclaration(30) with { LicenseValidThroughReturn = false },
            "documents" => ValidDeclaration(30) with { DocumentsAvailableAtPickup = false },
            _ => null
        };
        var key = Guid.NewGuid().ToString("N");
        using var rejected = await SendReservationAsync(
            request with { Driver = new DriverInfoRequest { Declaration = declaration } }, session, key, unpaid);
        rejected.StatusCode.Should().Be(expected, await rejected.Content.ReadAsStringAsync());
        (await WithDbContextAsync(db => db.Reservations.CountAsync())).Should().Be(0);

        if (invalid != "minimum-age")
        {
            using var retried = await SendReservationAsync(request, session, key, unpaid);
            retried.StatusCode.Should().Be(HttpStatusCode.OK, await retried.Content.ReadAsStringAsync());
            var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
            saved.DriverDeclaration.Should().BeEquivalentTo(request.Driver!.Declaration);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupQuoteCreate_AcceptsMinimumEligibility(bool unpaid)
    {
        var session = Guid.NewGuid().ToString("N");
        var request = await CreateQuotedRequestAsync(21, session);
        request = request with
        {
            Driver = new DriverInfoRequest { Declaration = ValidDeclaration(21) with { LicenseYearsAtPickup = 2 } }
        };
        using var response = await SendReservationAsync(request, session, Guid.NewGuid().ToString("N"), unpaid);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        saved.DriverDeclaration.Should().BeEquivalentTo(request.Driver!.Declaration);
        saved.PricingSnapshot!.BookingConditions.Should().BeNull();
        saved.QuoteReplayProof!.SchemaVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyUnquotedCreate_PreservesExistingNoDeclarationContract(bool unpaid)
    {
        var request = Request(30) with { Driver = null };
        using var response = await SendReservationAsync(request, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), unpaid);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = await WithDbContextAsync(db => db.Reservations.AsNoTracking().SingleAsync());
        saved.DriverDeclaration.Should().BeNull();
        saved.QuoteReplayProof.Should().BeNull();
    }

    private async Task<CreateReservationRequest> CreateQuotedRequestAsync(int age, string session)
    {
        var request = Request(age);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pricing/quote")
        {
            Content = JsonContent.Create(new CreateReservationQuoteRequest
            {
                VehicleGroupId = request.VehicleGroupId, PickupOfficeId = request.PickupOfficeId,
                ReturnOfficeId = request.ReturnOfficeId, PickupDateTimeUtc = request.PickupDateTimeUtc,
                ReturnDateTimeUtc = request.ReturnDateTimeUtc, DriverAge = age
            })
        };
        message.Headers.Add("X-Session-Id", session);
        using var response = await Client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return request with { QuoteId = json.RootElement.GetProperty("data").GetProperty("quoteId").GetGuid() };
    }

    private static CreateReservationRequest Request(int age)
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        return new CreateReservationRequest
        {
            VehicleGroupId = TestDataSeeder.GroupOneId, PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId, PickupDateTimeUtc = pickup, ReturnDateTimeUtc = pickup.AddDays(3),
            DriverAge = age, Driver = new DriverInfoRequest { Declaration = ValidDeclaration(age) },
            Customer = new CustomerInfoRequest
            {
                FirstName = "Synthetic", LastName = "Group", Email = "group@example.test", Phone = "+900000000000"
            }
        };
    }

    private static DriverDeclaration ValidDeclaration(int age) => new()
    {
        AgeAtPickup = age, LicenseYearsAtPickup = 8,
        LicenseValidThroughReturn = true, DocumentsAvailableAtPickup = true
    };

    private async Task<HttpResponseMessage> SendReservationAsync(CreateReservationRequest request, string session, string key, bool unpaid)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, unpaid ? "/api/v1/reservations/unpaid-requests" : "/api/v1/reservations")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Session-Id", session);
        message.Headers.Add("Idempotency-Key", key);
        return await Client.SendAsync(message);
    }
}
