using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.API.Contracts.Payments;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services.Payments;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PaymentServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACarDbContext _dbContext;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _dbContext = _dbFactory.CreateContext();
        SeedFeatureFlag();

        _sut = new PaymentService(
            _dbContext,
            new FakePaymentProvider(),
            Options.Create(new PaymentOptions
            {
                Provider = "Mock",
                Currency = "TRY",
                RetryLimit = 3,
                TimeoutRetryCount = 2,
                WebhookJobBatchSize = 10
            }),
            NullLogger<PaymentService>.Instance);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenCalled_QueuesBackgroundJobWithoutProcessingImmediately()
    {
        var paymentIntent = await SeedSucceededPaymentIntentAsync();

        var result = await _sut.ProcessWebhookAsync(
            "Mock",
            $$"""{"provider_event_id":"evt-1","payment_intent_id":"{{paymentIntent.ProviderIntentId}}","provider_transaction_id":"{{paymentIntent.ProviderTransactionId}}","event_type":"payment.succeeded"}""",
            "valid-signature",
            null,
            "payment.succeeded",
            CancellationToken.None);

        result.Duplicate.Should().BeFalse();
        result.Processed.Should().BeFalse();
        _dbContext.BackgroundJobs.Should().ContainSingle(x => x.Type == "payment-webhook-process");
        _dbContext.PaymentWebhookEvents.Should().ContainSingle(x => x.ProviderEventId == "evt-1" && !x.Processed);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenDuplicateWebhookHasFailedJob_QueuesReplacementJob()
    {
        var paymentIntent = await SeedSucceededPaymentIntentAsync();
        _dbContext.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            ProviderEventId = "evt-1",
            Payload = "{}",
            Processed = false
        });
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = "payment-webhook-process",
            Payload = $$"""{"ProviderEventId":"evt-1","EventType":"payment.succeeded","ProviderIntentId":"{{paymentIntent.ProviderIntentId}}","ProviderTransactionId":"{{paymentIntent.ProviderTransactionId}}","RawPayload":"{}"}""",
            Status = BackgroundJobStatus.Failed,
            RetryCount = 3,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ProcessWebhookAsync(
            "Mock",
            $$"""{"provider_event_id":"evt-1","payment_intent_id":"{{paymentIntent.ProviderIntentId}}","provider_transaction_id":"{{paymentIntent.ProviderTransactionId}}","event_type":"payment.succeeded"}""",
            "valid-signature",
            null,
            "payment.succeeded",
            CancellationToken.None);

        result.Duplicate.Should().BeTrue();
        _dbContext.BackgroundJobs
            .Count(x => x.Type == "payment-webhook-process" && x.Payload.Contains("\"ProviderEventId\":\"evt-1\""))
            .Should().Be(2);
        _dbContext.BackgroundJobs
            .Any(x => x.Type == "payment-webhook-process" && x.Status == BackgroundJobStatus.Pending && x.Payload.Contains("\"ProviderEventId\":\"evt-1\""))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPendingWebhookJobsAsync_WhenQueuedWebhookExists_UpdatesPaymentAndReservation()
    {
        var reservation = new Reservation
        {
            PublicCode = "RSV-001",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(2),
            ReturnDateTime = DateTime.UtcNow.AddDays(4),
            Status = ReservationStatus.PendingPayment,
            TotalAmount = 2500m
        };

        var paymentIntent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = reservation.TotalAmount,
            Status = PaymentStatus.Pending,
            Provider = "Mock",
            IdempotencyKey = "intent-1",
            ProviderIntentId = "provider-intent-1",
            ProviderTransactionId = "provider-tx-1"
        };

        _dbContext.Reservations.Add(reservation);
        _dbContext.PaymentIntents.Add(paymentIntent);
        _dbContext.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            ProviderEventId = "evt-2",
            Payload = "{}",
            Processed = false
        });
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = "payment-webhook-process",
            Payload = $$"""{"ProviderEventId":"evt-2","EventType":"payment.succeeded","ProviderIntentId":"{{paymentIntent.ProviderIntentId}}","ProviderTransactionId":"{{paymentIntent.ProviderTransactionId}}","RawPayload":"{}"}""",
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var processedCount = await _sut.ProcessPendingWebhookJobsAsync();

        processedCount.Should().Be(1);
        paymentIntent.Status.Should().Be(PaymentStatus.Succeeded);
        reservation.Status.Should().Be(ReservationStatus.Paid);
        _dbContext.PaymentWebhookEvents.Single(x => x.ProviderEventId == "evt-2").Processed.Should().BeTrue();
        _dbContext.BackgroundJobs.Single().Status.Should().Be(BackgroundJobStatus.Completed);
    }

    [Fact]
    public async Task ProcessPendingWebhookJobsAsync_WhenWebhookCannotBeMatched_KeepsJobPendingForRetry()
    {
        _dbContext.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            ProviderEventId = "evt-unmatched",
            Payload = "{}",
            Processed = false
        });
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = "payment-webhook-process",
            Payload = """{"ProviderEventId":"evt-unmatched","EventType":"payment.succeeded","ProviderIntentId":"missing-intent","ProviderTransactionId":"missing-tx","RawPayload":"{}"}""",
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var processedCount = await _sut.ProcessPendingWebhookJobsAsync();

        processedCount.Should().Be(0);
        var job = _dbContext.BackgroundJobs.Single();
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.RetryCount.Should().Be(1);
        _dbContext.PaymentWebhookEvents.Single(x => x.ProviderEventId == "evt-unmatched").Processed.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseDepositAsync_WhenNoDepositIntentExists_ReturnsSkippedOperation()
    {
        var reservation = await SeedReservationWithDepositAmountAsync(500m);

        var result = await _sut.ReleaseDepositAsync(reservation.Id, "checkout");

        result.Should().NotBeNull();
        result!.Operation.Should().Be("ReleaseDeposit");
        result.Status.Should().Be("Skipped");
        result.PaymentIntentId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task RefundReservationAsync_WhenIntentAlreadyRefunded_Throws()
    {
        var reservation = new Reservation
        {
            PublicCode = "RSV-REFUND-1",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2),
            Status = ReservationStatus.Cancelled,
            TotalAmount = 1000m
        };
        var refundedIntent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = 1000m,
            Status = PaymentStatus.Refunded,
            Provider = "Mock",
            IdempotencyKey = "refund-intent",
            ProviderIntentId = "provider-intent-refund",
            ProviderTransactionId = "provider-tx-refund"
        };

        _dbContext.Reservations.Add(reservation);
        _dbContext.PaymentIntents.Add(refundedIntent);
        await _dbContext.SaveChangesAsync();

        var action = () => _sut.RefundReservationAsync(
            reservation.Id,
            new AdminRefundApiRequest { Amount = 100m, Reason = "manual" },
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("İade işlemi için başarılı bir ödeme bulunamadı.");
    }

    [Fact]
    public async Task RefundReservationAsync_WhenRefundSucceeds_MarksIntentAsRefunded()
    {
        var reservation = new Reservation
        {
            PublicCode = "RSV-REFUND-2",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(1),
            ReturnDateTime = DateTime.UtcNow.AddDays(2),
            Status = ReservationStatus.Paid,
            TotalAmount = 1000m
        };
        var successfulIntent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = 1000m,
            Status = PaymentStatus.Succeeded,
            Provider = "Mock",
            IdempotencyKey = "refund-intent-2",
            ProviderIntentId = "provider-intent-refund-2",
            ProviderTransactionId = "provider-tx-refund-2"
        };

        _dbContext.Reservations.Add(reservation);
        _dbContext.PaymentIntents.Add(successfulIntent);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.RefundReservationAsync(
            reservation.Id,
            new AdminRefundApiRequest { Amount = 600m, Reason = "manual" },
            CancellationToken.None);

        result.Should().NotBeNull();
        successfulIntent.Status.Should().Be(PaymentStatus.Refunded);
    }

    private void SeedFeatureFlag()
    {
        _dbContext.FeatureFlags.Add(new FeatureFlag
        {
            Name = "EnableOnlinePayment",
            Enabled = true,
            Description = "test"
        });
        _dbContext.SaveChanges();
    }

    private async Task<PaymentIntent> SeedSucceededPaymentIntentAsync()
    {
        var reservation = new Reservation
        {
            PublicCode = "RSV-INTENT",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(3),
            ReturnDateTime = DateTime.UtcNow.AddDays(5),
            Status = ReservationStatus.Paid,
            TotalAmount = 1800m
        };
        var paymentIntent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = reservation.TotalAmount,
            Status = PaymentStatus.Succeeded,
            Provider = "Mock",
            IdempotencyKey = "seed-intent",
            ProviderIntentId = "seed-provider-intent",
            ProviderTransactionId = "seed-provider-tx"
        };

        _dbContext.Reservations.Add(reservation);
        _dbContext.PaymentIntents.Add(paymentIntent);
        await _dbContext.SaveChangesAsync();
        return paymentIntent;
    }

    private async Task<Reservation> SeedReservationWithDepositAmountAsync(decimal depositAmount)
    {
        var vehicleGroup = new VehicleGroup
        {
            NameTr = "Test Group",
            DepositAmount = depositAmount
        };
        var vehicle = new Vehicle
        {
            Plate = $"34TST{Random.Shared.Next(1000, 9999)}",
            Brand = "Test",
            Model = "Car",
            Year = 2024,
            Color = "White",
            OfficeId = Guid.NewGuid(),
            Group = vehicleGroup
        };
        var reservation = new Reservation
        {
            PublicCode = "RSV-DEP",
            CustomerId = Guid.NewGuid(),
            Vehicle = vehicle,
            PickupDateTime = DateTime.UtcNow.AddDays(-3),
            ReturnDateTime = DateTime.UtcNow.AddDays(-1),
            Status = ReservationStatus.Active,
            TotalAmount = 1500m
        };

        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync();
        return reservation;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _dbFactory.Dispose();
    }

    private sealed class FakePaymentProvider : IPaymentProvider
    {
        public Task<PaymentIntentProviderResult> CreatePaymentIntentAsync(CreatePaymentIntentProviderRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentIntentProviderResult
            {
                ProviderIntentId = Guid.NewGuid().ToString("N"),
                ProviderTransactionId = $"tx-{Guid.NewGuid():N}",
                Status = PaymentProviderIntentStatus.Pending3DS,
                RedirectUrl = "https://mock/3ds",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });

        public Task<PreAuthorizationProviderResult> CreatePreAuthorizationAsync(CreatePreAuthorizationProviderRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreAuthorizationProviderResult
            {
                ProviderIntentId = Guid.NewGuid().ToString("N"),
                ProviderTransactionId = $"deposit-{Guid.NewGuid():N}",
                Status = PaymentProviderIntentStatus.Authorized,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });

        public Task<PaymentVerificationProviderResult> VerifyPaymentAsync(PaymentCallbackProviderRequest callback, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentVerificationProviderResult
            {
                Status = PaymentProviderIntentStatus.Succeeded,
                TransactionId = $"tx-{Guid.NewGuid():N}"
            });

        public bool VerifyWebhookSignature(string payload, string signature, string? timestamp) => signature == "valid-signature";

        public Task<ParsedWebhookEvent> ParseWebhookAsync(string provider, string payload, string? eventType, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ParsedWebhookEvent
            {
                ProviderEventId = payload.Contains("evt-1") ? "evt-1" : "evt-2",
                EventType = eventType ?? "payment.succeeded",
                ProviderIntentId = ExtractValue(payload, "payment_intent_id", "providerIntentId"),
                ProviderTransactionId = ExtractValue(payload, "provider_transaction_id", "providerTransactionId"),
                RawPayload = payload
            });

        public Task<ProviderTransactionStatus> GetTransactionStatusAsync(string transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProviderTransactionStatus.Succeeded);

        public Task<ProviderRefundResult> RefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderRefundResult { Success = true, ReferenceId = "refund-1" });

        public Task<ProviderReleaseDepositResult> ReleaseDepositAsync(ProviderReleaseDepositRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderReleaseDepositResult { Success = true });

        public Task<ProviderCaptureDepositResult> CaptureDepositAsync(ProviderCaptureDepositRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderCaptureDepositResult { Success = true, ReferenceId = "capture-1" });

        private static string? ExtractValue(string payload, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var marker = $"\"{propertyName}\":\"";
                var start = payload.IndexOf(marker, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                start += marker.Length;
                var end = payload.IndexOf('"', start);
                if (end > start)
                {
                    return payload[start..end];
                }
            }

            return null;
        }
    }
}
