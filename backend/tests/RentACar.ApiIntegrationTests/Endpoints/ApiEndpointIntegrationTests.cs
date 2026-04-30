using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RentACar.API.Contracts.Auth;
using RentACar.API.Contracts.Payments;
using RentACar.API.Contracts.Reservations;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

/// <summary>
/// Covers the Phase 10.2 public and admin API endpoint integration smoke paths.
/// </summary>
public sealed class ApiEndpointIntegrationTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task CustomerRegister_WithValidPayload_ReturnsOk()
    {
        var request = new CustomerRegisterRequest(
            Email: $"customer-{Guid.NewGuid():N}@rentacar.test",
            Password: "IntegrationPassword123!",
            FullName: "Integration Customer",
            Phone: "+90 555 000 00 01");

        var response = await Client.PostAsJsonAsync("/api/customer/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CustomerLogin_WithValidCredentials_ReturnsOk()
    {
        var email = $"login-{Guid.NewGuid():N}@rentacar.test";
        var password = "IntegrationPassword123!";
        await Client.PostAsJsonAsync("/api/customer/v1/auth/register", new CustomerRegisterRequest(
            email,
            password,
            "Login Customer",
            "+90 555 000 00 02"));

        var response = await Client.PostAsJsonAsync("/api/customer/v1/auth/login", new CustomerLoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VehiclesAvailable_WithValidQuery_ReturnsOk()
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var dropoff = pickup.AddDays(3);
        var url = $"/api/v1/vehicles/available?office_id={TestDataSeeder.OfficeOneId}&pickup_datetime={Uri.EscapeDataString(pickup.ToString("s"))}&return_datetime={Uri.EscapeDataString(dropoff.ToString("s"))}";

        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReservationCreate_WithValidPayload_ReturnsOk()
    {
        var response = await CreateReservationAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReservationHold_WithValidReservationAndSession_ReturnsOk()
    {
        var createResponse = await CreateReservationAsync();
        var reservationId = await ReadResponseDataIdAsync(createResponse);
        using var holdRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reservations/{reservationId}/hold");
        holdRequest.Headers.Add("X-Session-Id", $"session-{Guid.NewGuid():N}");

        var response = await Client.SendAsync(holdRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PaymentIntent_WithValidReservation_ReturnsOk()
    {
        var createResponse = await CreateReservationAsync();
        var reservationId = await ReadResponseDataIdAsync(createResponse);
        var request = new CreatePaymentIntentApiRequest
        {
            ReservationId = reservationId,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Card = ValidCard()
        };

        var response = await Client.PostAsJsonAsync("/api/v1/payments/intents", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PaymentWebhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook/iyzico")
        {
            Content = JsonContent.Create(new { eventId = Guid.NewGuid().ToString("N"), status = "success" })
        };
        request.Headers.Add("X-Webhook-Signature", "invalid-signature");
        request.Headers.Add("X-Webhook-Event", "payment.succeeded");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminReservationCancel_WithAdminToken_ReturnsOk()
    {
        var createResponse = await CreateReservationAsync();
        var reservationId = await ReadResponseDataIdAsync(createResponse);
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/admin/v1/reservations/{reservationId}/cancel", "Integration test cancellation");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> CreateReservationAsync()
    {
        var pickup = DateTime.UtcNow.Date.AddDays(10).AddHours(10);
        var request = new CreateReservationRequest
        {
            VehicleGroupId = TestDataSeeder.GroupOneId,
            PickupOfficeId = TestDataSeeder.OfficeOneId,
            ReturnOfficeId = TestDataSeeder.OfficeOneId,
            PickupDateTimeUtc = pickup,
            ReturnDateTimeUtc = pickup.AddDays(3),
            Customer = new CustomerInfoRequest
            {
                FirstName = "Integration",
                LastName = "Customer",
                Email = $"reservation-{Guid.NewGuid():N}@rentacar.test",
                Phone = "+90 555 000 00 03"
            },
            SessionId = $"session-{Guid.NewGuid():N}"
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservations")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        return await Client.SendAsync(message);
    }

    private static PaymentCardApiRequest ValidCard() => new()
    {
        HolderName = "Integration Customer",
        Number = "4111111111111111",
        ExpiryMonth = "12",
        ExpiryYear = DateTime.UtcNow.AddYears(1).Year.ToString(),
        Cvv = "123"
    };

    private static async Task<Guid> ReadResponseDataIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var id = document.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        return id;
    }
}
