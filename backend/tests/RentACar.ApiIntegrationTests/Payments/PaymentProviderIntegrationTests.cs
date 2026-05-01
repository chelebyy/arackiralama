using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.Payments;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services.Payments;
using RentACar.ApiIntegrationTests.Infrastructure;
using Xunit;

namespace RentACar.ApiIntegrationTests.Payments;

/// <summary>
/// Verifies payment provider mock behavior through the real service pipeline.
/// Uses the built-in <see cref="MockPaymentProvider"/> which is already registered in DI.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PaymentProviderIntegrationTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    #region Helpers

    private async Task<Reservation> SeedReservationAsync(
        ReservationStatus status = ReservationStatus.Hold,
        CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();

        var vehicle = await dbContext.Vehicles.FirstAsync(cancellationToken);

        var customer = new Customer
        {
            FullName = "Test Customer",
            Phone = "+90 555 123 45 67",
            Email = $"test-{Guid.NewGuid():N}@example.com",
            IdentityNumber = "12345678901",
            Nationality = "TR"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        var pickup = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        var dropoff = pickup.AddDays(3);

        var reservation = new Reservation
        {
            PublicCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            VehicleId = vehicle.Id,
            CustomerId = customer.Id,
            PickupDateTime = pickup,
            ReturnDateTime = dropoff,
            TotalAmount = 1500m,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return reservation;
    }

    private async Task<PaymentIntentApiDto?> CreatePaymentIntentAsync(
        Guid reservationId,
        string idempotencyKey = "test-key",
        string? cardHolder = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var request = new CreatePaymentIntentApiRequest
        {
            ReservationId = reservationId,
            IdempotencyKey = idempotencyKey,
            InstallmentCount = 1,
            Card = new PaymentCardApiRequest
            {
                HolderName = cardHolder ?? "Test User",
                Number = "4111111111111111",
                ExpiryMonth = "12",
                ExpiryYear = "30",
                Cvv = "123"
            }
        };
        return await paymentService.CreateIntentAsync(request, cancellationToken);
    }

    private async Task<PaymentIntentApiDto?> CompleteThreeDsAsync(
        Guid intentId,
        string bankResponse = "success",
        CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var request = new ThreeDsReturnApiRequest { BankResponse = bankResponse };
        return await paymentService.CompleteThreeDsAsync(intentId, request, cancellationToken);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<Reservation> SeedPaidReservationAsync(CancellationToken cancellationToken = default)
    {
        var reservation = await SeedReservationAsync(ReservationStatus.Hold, cancellationToken);
        var intent = await CreatePaymentIntentAsync(reservation.Id, cancellationToken: cancellationToken);
        intent.Should().NotBeNull();
        var completed = await CompleteThreeDsAsync(intent!.PaymentIntentId, "success", cancellationToken);
        completed.Should().NotBeNull();
        return reservation;
    }

    #endregion

    #region 10.2.4.3 Idempotency

    [Fact]
    public async Task CreateIntentAsync_ReturnsExistingIntent_WhenIdempotencyKeyReused()
    {
        var reservation = await SeedReservationAsync();
        var key = $"idempotency-{Guid.NewGuid():N}";

        var first = await CreatePaymentIntentAsync(reservation.Id, key);
        var second = await CreatePaymentIntentAsync(reservation.Id, key);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.PaymentIntentId.Should().Be(first!.PaymentIntentId);
    }

    #endregion

    #region 10.2.4.2 Failure Flow — Timeout Retry

    [Fact]
    public async Task CreateIntentAsync_RetriesOnProviderTimeout()
    {
        var reservation = await SeedReservationAsync();

        var act = async () => await CreatePaymentIntentAsync(reservation.Id, cardHolder: "timeout");

        await act.Should().ThrowAsync<TimeoutException>();
    }

    #endregion

    #region 10.2.4.1 Success Flow — 3D Secure

    [Fact]
    public async Task CompleteThreeDsAsync_TransitionsToPaid_OnSuccess()
    {
        var reservation = await SeedReservationAsync();
        var intent = await CreatePaymentIntentAsync(reservation.Id);
        intent.Should().NotBeNull();

        var result = await CompleteThreeDsAsync(intent!.PaymentIntentId, "success");

        result.Should().NotBeNull();
        result!.Status.Should().Be("Succeeded");

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var refreshed = await dbContext.Reservations.AsNoTracking()
            .FirstAsync(r => r.Id == reservation.Id);
        refreshed.Status.Should().Be(ReservationStatus.Paid);
    }

    [Fact]
    public async Task CompleteThreeDsAsync_ReturnsFailed_WhenBankResponseFail()
    {
        var reservation = await SeedReservationAsync();
        var intent = await CreatePaymentIntentAsync(reservation.Id);
        intent.Should().NotBeNull();

        var result = await CompleteThreeDsAsync(intent!.PaymentIntentId, "fail");

        result.Should().NotBeNull();
        result!.Status.Should().Be("Failed");
    }

    #endregion

    #region 10.2.4.5 Refund Flow

    [Fact]
    public async Task RefundReservationAsync_ProcessesRefund_WhenReservationPaid()
    {
        var reservation = await SeedReservationAsync();
        var intent = await CreatePaymentIntentAsync(reservation.Id);
        intent.Should().NotBeNull();
        var completed = await CompleteThreeDsAsync(intent!.PaymentIntentId, "success");
        completed.Should().NotBeNull();

        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var refundRequest = new AdminRefundApiRequest { Amount = 1500m, Reason = "Customer request" };
        var refund = await paymentService.RefundReservationAsync(reservation.Id, refundRequest);

        refund.Should().NotBeNull();
        refund!.Status.Should().Be("Succeeded");

        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var refreshed = await dbContext.Reservations.AsNoTracking()
            .FirstAsync(r => r.Id == reservation.Id);
        refreshed.Status.Should().Be(ReservationStatus.Cancelled);
    }

    #endregion

    #region 10.2.4.6 Deposit Lifecycle

    [Fact]
    public async Task CreateDepositPreAuthorizationAsync_AddsTrackedIntent_WhenCalled()
    {
        var reservation = await SeedPaidReservationAsync();

        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var result = await paymentService.CreateDepositPreAuthorizationAsync(reservation.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Authorized");

        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        var tracked = dbContext.ChangeTracker.Entries<PaymentIntent>()
            .Any(e => e.Entity.ReservationId == reservation.Id
                      && e.Entity.Provider.EndsWith(":Deposit", StringComparison.Ordinal));
        tracked.Should().BeTrue();
    }

    [Fact]
    public async Task CaptureDepositAsync_Captures_WhenDepositAuthorized()
    {
        var reservation = await SeedPaidReservationAsync();

        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var preauth = await paymentService.CreateDepositPreAuthorizationAsync(reservation.Id);
        preauth.Should().NotBeNull();

        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        await dbContext.SaveChangesAsync();

        var capture = await paymentService.CaptureDepositAsync(reservation.Id, 500m, note: null);
        capture.Should().NotBeNull();
        capture!.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task ReleaseDepositAsync_Releases_WhenDepositExists()
    {
        var reservation = await SeedPaidReservationAsync();

        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var preauth = await paymentService.CreateDepositPreAuthorizationAsync(reservation.Id);
        preauth.Should().NotBeNull();

        var dbContext = scope.ServiceProvider.GetRequiredService<RentACarDbContext>();
        await dbContext.SaveChangesAsync();

        var release = await paymentService.ReleaseDepositAsync(reservation.Id, note: null);
        release.Should().NotBeNull();
        release!.Status.Should().Be("Succeeded");
    }

    #endregion

    #region 10.2.4.4 Webhook Processing

    [Fact]
    public async Task ProcessWebhookAsync_ThrowsUnauthorized_OnInvalidSignature()
    {
        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var payload = "{\"event_type\":\"payment.success\"}";

        var act = async () => await paymentService.ProcessWebhookAsync(
            "Mock", payload, "invalid-signature", timestamp: null, eventType: null);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ProcessWebhookAsync_QueuesJob_OnValidSignature()
    {
        using var scope = Services.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var payload = "{\"event_type\":\"payment.success\"}";
        var signature = ComputeSignature(payload, "mock-webhook-secret");

        var result = await paymentService.ProcessWebhookAsync(
            "Mock", payload, signature, timestamp: null, eventType: null);

        result.Should().NotBeNull();
        result.Processed.Should().BeFalse();
    }

    #endregion
}
