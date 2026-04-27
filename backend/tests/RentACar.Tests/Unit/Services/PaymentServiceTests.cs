using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RentACar.API.Contracts.Payments;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services.Payments;
using RentACar.Tests.TestFixtures;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PaymentServiceTests : IDisposable
{
    private static readonly DateTime FixedPastPickupDateTime = new(2030, 1, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedPastReturnDateTime = new(2030, 1, 6, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedFuturePickupDateTime = new(2030, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedFutureReturnDateTime = new(2030, 1, 12, 10, 0, 0, DateTimeKind.Utc);

    private readonly TestDbContextFactory _dbFactory = new();
    private readonly RentACarDbContext _dbContext;
    private readonly PaymentService _sut;
    private readonly Mock<INotificationQueueService> _notificationQueueServiceMock = new();

    public PaymentServiceTests()
    {
        _dbContext = _dbFactory.CreateContext();
        SeedFeatureFlag();

        _sut = CreateSut();

        _notificationQueueServiceMock
            .Setup(x => x.EnqueueEmailAsync(It.IsAny<QueuedEmailNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _notificationQueueServiceMock
            .Setup(x => x.EnqueueSmsAsync(It.IsAny<QueuedSmsNotificationRequest>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task CreateIntentAsync_WhenFeatureFlagDisabled_ThrowsInvalidOperationException()
    {
        var reservation = await SeedReservationAsync();
        _dbContext.FeatureFlags.Single(x => x.Name == "EnableOnlinePayment").Enabled = false;
        await _dbContext.SaveChangesAsync();

        var action = () => _sut.CreateIntentAsync(CreatePaymentIntentRequest(reservation.Id, "disabled-flag"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Online ödeme şu anda aktif değil.");
    }

    [Fact]
    public async Task CreateIntentAsync_WhenIdempotencyKeyBelongsToDifferentReservation_ThrowsInvalidOperationException()
    {
        var reservation = await SeedReservationAsync();
        var otherReservation = await SeedReservationAsync();
        await SeedPaymentIntentAsync(otherReservation.Id, "shared-key", PaymentStatus.Pending);

        var action = () => _sut.CreateIntentAsync(CreatePaymentIntentRequest(reservation.Id, "shared-key"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Bu idempotency key farklı bir rezervasyon için zaten kullanılmış.");
    }

    [Fact]
    public async Task CreateIntentAsync_WhenMatchingIntentAlreadyExists_ReturnsExistingIntentWithoutCallingProvider()
    {
        var provider = new FakePaymentProvider();
        var sut = CreateSut(provider);
        var reservation = await SeedReservationAsync();
        var existingIntent = await SeedPaymentIntentAsync(
            reservation.Id,
            "same-key",
            PaymentStatus.Pending,
            providerIntentId: "existing-provider-intent",
            providerTransactionId: "existing-provider-transaction");

        var result = await sut.CreateIntentAsync(CreatePaymentIntentRequest(reservation.Id, "same-key"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentIntentId.Should().Be(existingIntent.Id);
        provider.CreatePaymentIntentCallCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateIntentAsync_WhenRetryLimitReached_ThrowsInvalidOperationException()
    {
        var reservation = await SeedReservationAsync();
        await SeedPaymentIntentAsync(reservation.Id, "attempt-1", PaymentStatus.Failed);
        await SeedPaymentIntentAsync(reservation.Id, "attempt-2", PaymentStatus.Failed);
        await SeedPaymentIntentAsync(reservation.Id, "attempt-3", PaymentStatus.Failed);

        var action = () => _sut.CreateIntentAsync(CreatePaymentIntentRequest(reservation.Id, "attempt-4"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Maksimum ödeme deneme sayısı aşıldı (3).");
    }

    [Fact]
    public async Task CreateIntentAsync_WhenRequestIsValid_PersistsNewIntentFromProviderResult()
    {
        var provider = new FakePaymentProvider
        {
            CreatePaymentIntentResult = new PaymentIntentProviderResult
            {
                ProviderIntentId = "provider-intent-create",
                ProviderTransactionId = "provider-transaction-create",
                Status = PaymentProviderIntentStatus.Pending3DS,
                RedirectUrl = "https://mock/create-intent",
                ExpiresAtUtc = FakePaymentProvider.FixedExpiryUtc
            }
        };
        var sut = CreateSut(provider);
        var reservation = await SeedReservationAsync();

        var result = await sut.CreateIntentAsync(CreatePaymentIntentRequest(reservation.Id, "new-intent"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentProviderIntentStatus.Pending3DS.ToString());
        result.RedirectUrl.Should().Be("https://mock/create-intent");
        result.Amount.Should().Be(reservation.TotalAmount);
        provider.CreatePaymentIntentCallCount.Should().Be(1);

        var storedIntent = _dbContext.PaymentIntents.Single(x => x.Id == result.PaymentIntentId);
        storedIntent.ProviderIntentId.Should().Be("provider-intent-create");
        storedIntent.ProviderTransactionId.Should().Be("provider-transaction-create");
        storedIntent.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task CompleteThreeDsAsync_WhenVerificationSucceeds_UpdatesIntentAndReservation()
    {
        var provider = new FakePaymentProvider
        {
            VerifyPaymentResult = new PaymentVerificationProviderResult
            {
                Status = PaymentProviderIntentStatus.Succeeded,
                TransactionId = "verified-transaction"
            }
        };
        var sut = CreateSut(provider);
        var reservation = await SeedReservationAsync();
        var intent = await SeedPaymentIntentAsync(
            reservation.Id,
            "three-ds-success",
            PaymentStatus.Pending,
            providerIntentId: "provider-intent-3ds");

        var result = await sut.CompleteThreeDsAsync(intent.Id, new ThreeDsReturnApiRequest { BankResponse = "ok" }, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentProviderIntentStatus.Succeeded.ToString());
        intent.Status.Should().Be(PaymentStatus.Succeeded);
        intent.ProviderTransactionId.Should().Be("verified-transaction");
        reservation.Status.Should().Be(ReservationStatus.Paid);
    }

    [Fact]
    public async Task CompleteThreeDsAsync_WhenVerificationFails_MarksIntentFailedWithoutPayingReservation()
    {
        var provider = new FakePaymentProvider
        {
            VerifyPaymentResult = new PaymentVerificationProviderResult
            {
                Status = PaymentProviderIntentStatus.Failed,
                TransactionId = "failed-transaction"
            }
        };
        var sut = CreateSut(provider);
        var reservation = await SeedReservationAsync();
        var intent = await SeedPaymentIntentAsync(
            reservation.Id,
            "three-ds-failure",
            PaymentStatus.Pending,
            providerIntentId: "provider-intent-failed");

        var result = await sut.CompleteThreeDsAsync(intent.Id, new ThreeDsReturnApiRequest { BankResponse = "fail" }, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentProviderIntentStatus.Failed.ToString());
        intent.Status.Should().Be(PaymentStatus.Failed);
        reservation.Status.Should().Be(ReservationStatus.PendingPayment);
    }

    [Fact]
    public async Task RetryPaymentAsync_WhenIdempotencyKeyIsMissing_GeneratesRetryKeyForProviderRequest()
    {
        var provider = new FakePaymentProvider();
        var sut = CreateSut(provider);
        var reservation = await SeedReservationAsync();

        var result = await sut.RetryPaymentAsync(
            new AdminPaymentRetryApiRequest
            {
                ReservationId = reservation.Id,
                Card = CreateCardRequest()
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        provider.CreatePaymentIntentCallCount.Should().Be(1);
        provider.LastCreatePaymentIntentRequest!.IdempotencyKey.Should().StartWith("retry-");
    }

    [Fact]
    public async Task CreateDepositPreAuthorizationAsync_WhenSuccessfulMainPaymentExists_CreatesAuthorizedDepositIntent()
    {
        var provider = new FakePaymentProvider
        {
            CreatePreAuthorizationResult = new PreAuthorizationProviderResult
            {
                ProviderIntentId = "deposit-provider-intent",
                ProviderTransactionId = "deposit-provider-transaction",
                Status = PaymentProviderIntentStatus.Authorized,
                ExpiresAtUtc = FakePaymentProvider.FixedExpiryUtc
            }
        };
        var sut = CreateSut(provider);
        var reservation = await SeedReservationWithDepositAmountAsync(750m, ReservationStatus.Paid);
        await SeedPaymentIntentAsync(
            reservation.Id,
            "main-payment",
            PaymentStatus.Succeeded,
            providerIntentId: "main-provider-intent",
            providerTransactionId: "main-provider-transaction");

        var result = await sut.CreateDepositPreAuthorizationAsync(reservation.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.PaymentKind.Should().Be("DepositPreAuthorization");
        result.Status.Should().Be(PaymentStatus.Authorized.ToString());
        result.Amount.Should().Be(750m);
        provider.CreatePreAuthorizationCallCount.Should().Be(1);
        provider.LastCreatePreAuthorizationRequest!.ReferenceTransactionId.Should().Be("main-provider-transaction");

        var trackedDepositIntent = _dbContext.ChangeTracker
            .Entries<PaymentIntent>()
            .Single(x =>
                x.Entity.ReservationId == reservation.Id
                && x.Entity.Provider == "Mock:Deposit"
                && x.Entity.Amount == 750m)
            .Entity;

        trackedDepositIntent.Status.Should().Be(PaymentStatus.Authorized);
    }

    [Fact]
    public async Task CaptureDepositAsync_WhenAuthorizedDepositExists_CapturesDepositAndMarksIntentSucceeded()
    {
        var provider = new FakePaymentProvider
        {
            CaptureDepositResult = new ProviderCaptureDepositResult
            {
                Success = true,
                ReferenceId = "capture-reference"
            }
        };
        var sut = CreateSut(provider);
        var reservation = await SeedReservationAsync(status: ReservationStatus.Completed);
        var depositIntent = await SeedPaymentIntentAsync(
            reservation.Id,
            "deposit-intent",
            PaymentStatus.Authorized,
            provider: "Mock:Deposit",
            amount: 500m,
            providerIntentId: "deposit-provider-intent",
            providerTransactionId: "deposit-provider-transaction");

        var result = await sut.CaptureDepositAsync(reservation.Id, 250m, "damage", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Succeeded");
        result.ReferenceId.Should().Be("capture-reference");
        depositIntent.Status.Should().Be(PaymentStatus.Succeeded);
        provider.CaptureDepositCallCount.Should().Be(1);
        provider.LastCaptureDepositRequest!.Amount.Should().Be(250m);
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
            .Count(x => x.Type == "payment-webhook-process" && WebhookJobPayloadMatcher.HasProviderEventId(x.Payload, "evt-1"))
            .Should().Be(2);
        _dbContext.BackgroundJobs
            .Any(x => x.Type == "payment-webhook-process" && x.Status == BackgroundJobStatus.Pending && WebhookJobPayloadMatcher.HasProviderEventId(x.Payload, "evt-1"))
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
        _dbContext.Customers.Add(new Customer
        {
            Id = reservation.CustomerId,
            FullName = "Test Customer",
            Email = "customer@example.com",
            Phone = "+905551112233"
        });

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
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.ReservationConfirmed),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.PaymentReceived),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.PickupReminder),
                It.Is<DateTime?>(d => d == reservation.PickupDateTime.AddHours(-24)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueSmsAsync(
                It.Is<QueuedSmsNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.ReturnReminder),
                It.Is<DateTime?>(d => d == reservation.ReturnDateTime.AddHours(-24)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReleaseDepositAsync_WhenDepositReleased_QueuesDepositReleasedNotifications()
    {
        var customerId = Guid.NewGuid();
        var reservation = new Reservation
        {
            PublicCode = "RSV-DEPOSIT",
            CustomerId = customerId,
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(-2),
            ReturnDateTime = DateTime.UtcNow.AddDays(1),
            Status = ReservationStatus.Completed,
            TotalAmount = 1500m
        };
        var depositIntent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = 500m,
            Status = PaymentStatus.Authorized,
            Provider = "Mock:Deposit",
            IdempotencyKey = "deposit-intent",
            ProviderIntentId = "deposit-provider-intent",
            ProviderTransactionId = "deposit-provider-tx"
        };

        _dbContext.Customers.Add(new Customer
        {
            Id = customerId,
            FullName = "Deposit Customer",
            Email = "deposit@example.com",
            Phone = "+905551119988"
        });
        _dbContext.Reservations.Add(reservation);
        _dbContext.PaymentIntents.Add(depositIntent);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ReleaseDepositAsync(reservation.Id, "checkout", CancellationToken.None);

        result.Should().NotBeNull();
        depositIntent.Status.Should().Be(PaymentStatus.Cancelled);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.DepositReleased),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueSmsAsync(
                It.Is<QueuedSmsNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.DepositReleased),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingWebhookJobsAsync_WhenPaymentSucceeds_QueuesReminderNotifications()
    {
        var reservation = new Reservation
        {
            PublicCode = "RSV-REMINDERS",
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = DateTime.UtcNow.AddDays(3),
            ReturnDateTime = DateTime.UtcNow.AddDays(5),
            Status = ReservationStatus.PendingPayment,
            TotalAmount = 2500m
        };
        _dbContext.Customers.Add(new Customer
        {
            Id = reservation.CustomerId,
            FullName = "Reminder Customer",
            Email = "reminder@example.com",
            Phone = "+905551110000"
        });
        var paymentIntent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = reservation.TotalAmount,
            Status = PaymentStatus.Pending,
            Provider = "Mock",
            IdempotencyKey = "intent-reminders",
            ProviderIntentId = "provider-intent-reminders",
            ProviderTransactionId = "provider-tx-reminders"
        };

        _dbContext.Reservations.Add(reservation);
        _dbContext.PaymentIntents.Add(paymentIntent);
        _dbContext.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            ProviderEventId = "evt-reminders",
            Payload = "{}",
            Processed = false
        });
        _dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = "payment-webhook-process",
            Payload = $$"""{"ProviderEventId":"evt-reminders","EventType":"payment.succeeded","ProviderIntentId":"{{paymentIntent.ProviderIntentId}}","ProviderTransactionId":"{{paymentIntent.ProviderTransactionId}}","RawPayload":"{}"}""",
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        await _sut.ProcessPendingWebhookJobsAsync();

        _notificationQueueServiceMock.Verify(
            x => x.EnqueueEmailAsync(
                It.Is<QueuedEmailNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.PickupReminder),
                It.Is<DateTime?>(d => d == reservation.PickupDateTime.AddHours(-24)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationQueueServiceMock.Verify(
            x => x.EnqueueSmsAsync(
                It.Is<QueuedSmsNotificationRequest>(r => r.TemplateKey == NotificationTemplateKeys.ReturnReminder),
                It.Is<DateTime?>(d => d == reservation.ReturnDateTime.AddHours(-24)),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

    private PaymentService CreateSut(FakePaymentProvider? paymentProvider = null)
    {
        return new PaymentService(
            _dbContext,
            paymentProvider ?? new FakePaymentProvider(),
            _notificationQueueServiceMock.Object,
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

    private static CreatePaymentIntentApiRequest CreatePaymentIntentRequest(Guid reservationId, string idempotencyKey)
    {
        return new CreatePaymentIntentApiRequest
        {
            ReservationId = reservationId,
            IdempotencyKey = idempotencyKey,
            InstallmentCount = 1,
            Card = CreateCardRequest()
        };
    }

    private static PaymentCardApiRequest CreateCardRequest()
    {
        return new PaymentCardApiRequest
        {
            HolderName = "Test User",
            Number = "4111111111111111",
            ExpiryMonth = "12",
            ExpiryYear = "2030",
            Cvv = "123"
        };
    }

    private async Task<Reservation> SeedReservationAsync(
        ReservationStatus status = ReservationStatus.PendingPayment,
        decimal totalAmount = 2500m)
    {
        var reservation = new Reservation
        {
            PublicCode = $"RSV-{Guid.NewGuid():N}"[..12],
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            PickupDateTime = FixedFuturePickupDateTime,
            ReturnDateTime = FixedFutureReturnDateTime,
            Status = status,
            TotalAmount = totalAmount
        };

        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync();
        return reservation;
    }

    private async Task<PaymentIntent> SeedPaymentIntentAsync(
        Guid reservationId,
        string idempotencyKey,
        PaymentStatus status,
        string provider = "Mock",
        decimal? amount = null,
        string? providerIntentId = null,
        string? providerTransactionId = null)
    {
        var paymentIntent = new PaymentIntent
        {
            ReservationId = reservationId,
            Amount = amount ?? 2500m,
            Status = status,
            Provider = provider,
            IdempotencyKey = idempotencyKey,
            ProviderIntentId = providerIntentId,
            ProviderTransactionId = providerTransactionId
        };

        _dbContext.PaymentIntents.Add(paymentIntent);
        await _dbContext.SaveChangesAsync();
        return paymentIntent;
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

    private async Task<Reservation> SeedReservationWithDepositAmountAsync(
        decimal depositAmount,
        ReservationStatus status = ReservationStatus.Active)
    {
        var vehicleGroup = new VehicleGroup
        {
            NameTr = "Test Group",
            DepositAmount = depositAmount
        };
        var vehicle = new Vehicle
        {
            Plate = $"34TST{_dbContext.Vehicles.Count() + 1000}",
            Brand = "Test",
            Model = "Car",
            Year = 2024,
            Color = "White",
            OfficeId = Guid.NewGuid(),
            GroupId = vehicleGroup.Id,
            Group = vehicleGroup
        };
        var reservation = new Reservation
        {
            PublicCode = "RSV-DEP",
            CustomerId = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            PickupDateTime = FixedPastPickupDateTime,
            ReturnDateTime = FixedPastReturnDateTime,
            Status = status,
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
        public static readonly DateTime FixedExpiryUtc = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public int CreatePaymentIntentCallCount { get; private set; }
        public int CreatePreAuthorizationCallCount { get; private set; }
        public int VerifyPaymentCallCount { get; private set; }
        public int CaptureDepositCallCount { get; private set; }
        public CreatePaymentIntentProviderRequest? LastCreatePaymentIntentRequest { get; private set; }
        public CreatePreAuthorizationProviderRequest? LastCreatePreAuthorizationRequest { get; private set; }
        public PaymentCallbackProviderRequest? LastVerifyPaymentRequest { get; private set; }
        public ProviderCaptureDepositRequest? LastCaptureDepositRequest { get; private set; }
        public PaymentIntentProviderResult CreatePaymentIntentResult { get; set; } = new()
        {
            ProviderIntentId = "provider-intent-1",
            ProviderTransactionId = "provider-transaction-1",
            Status = PaymentProviderIntentStatus.Pending3DS,
            RedirectUrl = "https://mock/3ds",
            ExpiresAtUtc = FixedExpiryUtc
        };
        public PreAuthorizationProviderResult CreatePreAuthorizationResult { get; set; } = new()
        {
            ProviderIntentId = "deposit-intent-1",
            ProviderTransactionId = "deposit-transaction-1",
            Status = PaymentProviderIntentStatus.Authorized,
            ExpiresAtUtc = FixedExpiryUtc
        };
        public PaymentVerificationProviderResult VerifyPaymentResult { get; set; } = new()
        {
            Status = PaymentProviderIntentStatus.Succeeded,
            TransactionId = "verified-transaction-1"
        };
        public ProviderCaptureDepositResult CaptureDepositResult { get; set; } = new()
        {
            Success = true,
            ReferenceId = "capture-1"
        };

        public Task<PaymentIntentProviderResult> CreatePaymentIntentAsync(CreatePaymentIntentProviderRequest request, CancellationToken cancellationToken = default)
        {
            CreatePaymentIntentCallCount++;
            LastCreatePaymentIntentRequest = request;
            return Task.FromResult(CreatePaymentIntentResult);
        }

        public Task<PreAuthorizationProviderResult> CreatePreAuthorizationAsync(CreatePreAuthorizationProviderRequest request, CancellationToken cancellationToken = default)
        {
            CreatePreAuthorizationCallCount++;
            LastCreatePreAuthorizationRequest = request;
            return Task.FromResult(CreatePreAuthorizationResult);
        }

        public Task<PaymentVerificationProviderResult> VerifyPaymentAsync(PaymentCallbackProviderRequest callback, CancellationToken cancellationToken = default)
        {
            VerifyPaymentCallCount++;
            LastVerifyPaymentRequest = callback;
            return Task.FromResult(VerifyPaymentResult);
        }

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

        public Task<ProviderCaptureDepositResult> CaptureDepositAsync(ProviderCaptureDepositRequest request, CancellationToken cancellationToken = default)
        {
            CaptureDepositCallCount++;
            LastCaptureDepositRequest = request;
            return Task.FromResult(CaptureDepositResult);
        }

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
