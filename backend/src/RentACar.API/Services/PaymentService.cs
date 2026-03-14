using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentACar.API.Contracts.Payments;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Payments;
using RentACar.Infrastructure.Services.Payments;

namespace RentACar.API.Services;

public sealed class PaymentService(
    IApplicationDbContext dbContext,
    IPaymentProvider paymentProvider,
    IOptions<PaymentOptions> paymentOptions,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const string DepositProviderSuffix = ":Deposit";
    private const string WebhookProcessingJobType = "payment-webhook-process";

    private readonly IApplicationDbContext _dbContext = dbContext;
    private readonly IPaymentProvider _paymentProvider = paymentProvider;
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;
    private readonly ILogger<PaymentService> _logger = logger;

    public async Task<PaymentIntentApiDto?> CreateIntentAsync(
        CreatePaymentIntentApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == request.ReservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        EnsureOnlinePaymentEnabled();

        var conflictingIntent = await _dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Provider == _paymentOptions.Provider
                    && x.IdempotencyKey == request.IdempotencyKey
                    && x.ReservationId != request.ReservationId,
                cancellationToken);
        if (conflictingIntent != null)
        {
            throw new InvalidOperationException("Bu idempotency key farklı bir rezervasyon için zaten kullanılmış.");
        }

        var existingIntent = await _dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Provider == _paymentOptions.Provider
                    && x.ReservationId == request.ReservationId
                    && x.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
        if (existingIntent != null)
        {
            return MapIntentToDto(existingIntent, reservation.Status.ToString());
        }

        await EnsureRetryLimitAsync(reservation.Id, cancellationToken);

        if (reservation.Status == ReservationStatus.Hold)
        {
            reservation.Status = ReservationStatus.PendingPayment;
        }
        else if (reservation.Status != ReservationStatus.PendingPayment)
        {
            throw new InvalidOperationException("Bu rezervasyon için ödeme başlatılamaz.");
        }

        var providerResult = await ExecuteWithTimeoutRetryAsync(
            () => _paymentProvider.CreatePaymentIntentAsync(
                new CreatePaymentIntentProviderRequest
                {
                    ReservationId = reservation.Id,
                    Amount = reservation.TotalAmount,
                    Currency = _paymentOptions.Currency,
                    IdempotencyKey = request.IdempotencyKey,
                    InstallmentCount = request.InstallmentCount,
                    Card = new ProviderCardData
                    {
                        HolderName = request.Card.HolderName,
                        Number = request.Card.Number,
                        ExpiryMonth = request.Card.ExpiryMonth,
                        ExpiryYear = request.Card.ExpiryYear,
                        Cvv = request.Card.Cvv
                    }
                },
                cancellationToken),
            $"payment-intent:{reservation.Id}",
            cancellationToken);

        var intent = new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = reservation.TotalAmount,
            IdempotencyKey = request.IdempotencyKey,
            Provider = _paymentOptions.Provider,
            ProviderIntentId = providerResult.ProviderIntentId,
            ProviderTransactionId = providerResult.ProviderTransactionId,
            Status = MapToEntityStatus(providerResult.Status)
        };

        await _dbContext.PaymentIntents.AddAsync(intent, cancellationToken);
        reservation.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentIntentApiDto
        {
            PaymentIntentId = intent.Id,
            PaymentKind = GetPaymentKind(intent),
            Status = providerResult.Status.ToString(),
            RedirectUrl = providerResult.RedirectUrl,
            Amount = intent.Amount,
            Currency = _paymentOptions.Currency,
            ExpiresAt = providerResult.ExpiresAtUtc,
            TransactionId = providerResult.ProviderTransactionId,
            ReservationStatus = reservation.Status.ToString()
        };
    }

    public async Task<PaymentIntentApiDto?> CompleteThreeDsAsync(
        Guid intentId,
        ThreeDsReturnApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var intent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(x => x.Id == intentId, cancellationToken);
        if (intent == null)
        {
            return null;
        }

        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == intent.ReservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var verificationResult = await ExecuteWithTimeoutRetryAsync(
            () => _paymentProvider.VerifyPaymentAsync(
                new PaymentCallbackProviderRequest
                {
                    ProviderIntentId = GetRequiredProviderIntentId(intent),
                    BankResponse = request.BankResponse,
                    RawPayload = request.BankResponse
                },
                cancellationToken),
            $"3ds-verify:{GetRequiredProviderIntentId(intent)}",
            cancellationToken);

        intent.Status = MapToEntityStatus(verificationResult.Status);
        if (!string.IsNullOrWhiteSpace(verificationResult.TransactionId))
        {
            intent.ProviderTransactionId = verificationResult.TransactionId;
        }

        intent.UpdatedAt = DateTime.UtcNow;

        if (verificationResult.Status == PaymentProviderIntentStatus.Succeeded)
        {
            reservation.Status = ReservationStatus.Paid;
            reservation.UpdatedAt = DateTime.UtcNow;
            await EnsureDepositPreAuthorizationAsync(reservation, GetPreferredProviderReference(intent), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentIntentApiDto
        {
            PaymentIntentId = intent.Id,
            PaymentKind = GetPaymentKind(intent),
            Status = verificationResult.Status.ToString(),
            RedirectUrl = null,
            Amount = intent.Amount,
            Currency = _paymentOptions.Currency,
            ExpiresAt = intent.UpdatedAt,
            TransactionId = verificationResult.TransactionId,
            ReservationStatus = reservation.Status.ToString()
        };
    }

    public async Task<WebhookProcessApiDto> ProcessWebhookAsync(
        string provider,
        string payload,
        string signature,
        string? timestamp,
        string? eventType,
        CancellationToken cancellationToken = default)
    {
        if (!_paymentProvider.VerifyWebhookSignature(payload, signature, timestamp))
        {
            throw new UnauthorizedAccessException("Webhook imza doğrulaması başarısız.");
        }

        var parsedEvent = await _paymentProvider.ParseWebhookAsync(provider, payload, eventType, cancellationToken);
        var existingEvent = await _dbContext.PaymentWebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderEventId == parsedEvent.ProviderEventId, cancellationToken);

        if (existingEvent != null)
        {
            if (!existingEvent.Processed)
            {
                var providerEventMarker = $"\"ProviderEventId\":\"{parsedEvent.ProviderEventId}\"";
                var hasRunnableJob = await _dbContext.BackgroundJobs
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.Type == WebhookProcessingJobType
                            && x.Payload.Contains(providerEventMarker)
                            && (x.Status == BackgroundJobStatus.Pending || x.Status == BackgroundJobStatus.Processing),
                        cancellationToken);

                if (!hasRunnableJob)
                {
                    await _dbContext.BackgroundJobs.AddAsync(CreateWebhookProcessingJob(parsedEvent), cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return new WebhookProcessApiDto
            {
                ProviderEventId = parsedEvent.ProviderEventId,
                EventType = parsedEvent.EventType,
                Duplicate = true,
                Processed = existingEvent.Processed
            };
        }

        await _dbContext.PaymentWebhookEvents.AddAsync(new PaymentWebhookEvent
        {
            ProviderEventId = parsedEvent.ProviderEventId,
            Payload = parsedEvent.RawPayload,
            Processed = false
        }, cancellationToken);

        await _dbContext.BackgroundJobs.AddAsync(CreateWebhookProcessingJob(parsedEvent), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WebhookProcessApiDto
        {
            ProviderEventId = parsedEvent.ProviderEventId,
            EventType = parsedEvent.EventType,
            Duplicate = false,
            Processed = false
        };
    }

    public async Task<PaymentOperationApiDto?> RefundReservationAsync(
        Guid reservationId,
        AdminRefundApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var intent = await GetLatestRentalPaymentIntentAsync(reservationId, cancellationToken);
        if (intent == null || intent.Status is not (PaymentStatus.Succeeded or PaymentStatus.Authorized))
        {
            throw new InvalidOperationException("İade işlemi için başarılı bir ödeme bulunamadı.");
        }

        var refundAmount = request.Amount ?? CalculateRefundAmount(reservation, intent.Amount);
        var cancellationFee = request.Amount.HasValue ? 0m : Math.Max(intent.Amount - refundAmount, 0m);
        if (refundAmount <= 0 || refundAmount > intent.Amount)
        {
            throw new InvalidOperationException("Bu rezervasyon için para iadesine izin verilmiyor.");
        }

        var providerResult = await _paymentProvider.RefundAsync(
            new ProviderRefundRequest
            {
                ProviderIntentId = GetRequiredProviderIntentId(intent),
                Amount = refundAmount,
                Currency = _paymentOptions.Currency,
                Reason = request.Reason
            },
            cancellationToken);

        if (!providerResult.Success)
        {
            throw new InvalidOperationException(providerResult.FailureMessage ?? "İade işlemi başarısız.");
        }

        intent.Status = PaymentStatus.Refunded;

        if (reservation.Status is ReservationStatus.Paid or ReservationStatus.PendingPayment or ReservationStatus.Hold)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTime.UtcNow;
        }

        intent.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentOperationApiDto
        {
            ReservationId = reservationId,
            PaymentIntentId = intent.Id,
            PaymentKind = GetPaymentKind(intent),
            Operation = "Refund",
            Status = "Succeeded",
            Amount = refundAmount,
            Currency = _paymentOptions.Currency,
            ReferenceId = providerResult.ReferenceId,
            Reason = cancellationFee > 0
                ? $"{request.Reason ?? "Cancellation"} | CancellationFee={cancellationFee:0.00}"
                : request.Reason
        };
    }

    public async Task<PaymentOperationApiDto?> ReleaseDepositAsync(
        Guid reservationId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var depositIntent = await GetLatestDepositIntentAsync(reservationId, cancellationToken);
        if (depositIntent == null)
        {
            return BuildSkippedDepositOperation(
                reservationId,
                "ReleaseDeposit",
                string.IsNullOrWhiteSpace(note)
                    ? "Depozito ön provizyon kaydı bulunamadığı için bırakma işlemi atlandı."
                    : note);
        }

        var providerResult = await _paymentProvider.ReleaseDepositAsync(
            new ProviderReleaseDepositRequest
            {
                ProviderIntentId = GetRequiredProviderIntentId(depositIntent),
                Note = note
            },
            cancellationToken);

        if (!providerResult.Success)
        {
            throw new InvalidOperationException(providerResult.FailureMessage ?? "Depozito bırakma işlemi başarısız.");
        }

        depositIntent.Status = PaymentStatus.Cancelled;
        depositIntent.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentOperationApiDto
        {
            ReservationId = reservationId,
            PaymentIntentId = depositIntent.Id,
            PaymentKind = GetPaymentKind(depositIntent),
            Operation = "ReleaseDeposit",
            Status = "Succeeded",
            Amount = depositIntent.Amount,
            Currency = _paymentOptions.Currency,
            ReferenceId = null,
            Reason = note
        };
    }

    public async Task<PaymentOperationApiDto?> CreateDepositPreAuthorizationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var mainPaymentIntent = await GetLatestRentalPaymentIntentAsync(reservationId, cancellationToken);
        if (mainPaymentIntent == null || mainPaymentIntent.Status != PaymentStatus.Succeeded)
        {
            return BuildSkippedDepositOperation(
                reservationId,
                "CreatePreAuthorization",
                "Depozito ön provizyonu için başarılı online ödeme kaydı bulunamadı.");
        }

        var depositIntent = await EnsureDepositPreAuthorizationAsync(
            reservation,
            GetPreferredProviderReference(mainPaymentIntent),
            cancellationToken);

        if (depositIntent == null)
        {
            return BuildSkippedDepositOperation(reservationId, "CreatePreAuthorization", null);
        }

        return new PaymentOperationApiDto
        {
            ReservationId = reservationId,
            PaymentIntentId = depositIntent.Id,
            PaymentKind = GetPaymentKind(depositIntent),
            Operation = "CreatePreAuthorization",
            Status = depositIntent.Status.ToString(),
            Amount = depositIntent.Amount,
            Currency = _paymentOptions.Currency,
            ReferenceId = depositIntent.ProviderTransactionId ?? depositIntent.ProviderIntentId,
            Reason = null
        };
    }

    public async Task<PaymentOperationApiDto?> CaptureDepositAsync(
        Guid reservationId,
        decimal amount,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation == null)
        {
            return null;
        }

        var depositIntent = await GetLatestDepositIntentAsync(reservationId, cancellationToken);
        if (depositIntent == null || depositIntent.Status != PaymentStatus.Authorized)
        {
            throw new InvalidOperationException("Capture işlemi için yetkilenmiş depozito ön provizyonu bulunamadı.");
        }

        if (amount <= 0 || amount > depositIntent.Amount)
        {
            throw new InvalidOperationException("Capture tutarı geçersiz.");
        }

        var providerResult = await _paymentProvider.CaptureDepositAsync(
            new ProviderCaptureDepositRequest
            {
                ProviderIntentId = GetRequiredProviderIntentId(depositIntent),
                Amount = amount,
                Currency = _paymentOptions.Currency,
                Note = note
            },
            cancellationToken);

        if (!providerResult.Success)
        {
            throw new InvalidOperationException(providerResult.FailureMessage ?? "Depozito capture işlemi başarısız.");
        }

        depositIntent.Status = PaymentStatus.Succeeded;
        depositIntent.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentOperationApiDto
        {
            ReservationId = reservationId,
            PaymentIntentId = depositIntent.Id,
            PaymentKind = GetPaymentKind(depositIntent),
            Operation = "CapturePreAuthorization",
            Status = "Succeeded",
            Amount = amount,
            Currency = _paymentOptions.Currency,
            ReferenceId = providerResult.ReferenceId,
            Reason = note
        };
    }

    public async Task<PaymentIntentApiDto?> RetryPaymentAsync(
        AdminPaymentRetryApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ReservationId == Guid.Empty)
        {
            throw new InvalidOperationException("Geçerli bir rezervasyon ID gereklidir.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"retry-{Guid.NewGuid():N}"
            : request.IdempotencyKey;

        return await CreateIntentAsync(
            new CreatePaymentIntentApiRequest
            {
                ReservationId = request.ReservationId,
                IdempotencyKey = idempotencyKey,
                InstallmentCount = request.InstallmentCount,
                Card = request.Card
            },
            cancellationToken);
    }

    public async Task<AdminPaymentStatusApiDto?> GetPaymentStatusAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var intent = await _dbContext.PaymentIntents
            .FirstOrDefaultAsync(x => x.Id == paymentIntentId, cancellationToken);
        if (intent == null)
        {
            return null;
        }

        var providerStatus = await _paymentProvider.GetTransactionStatusAsync(
            GetPreferredProviderReference(intent),
            cancellationToken);

        var nextStatus = MapToEntityStatus(providerStatus, intent.Status);
        if (nextStatus != intent.Status)
        {
            intent.Status = nextStatus;
            intent.UpdatedAt = DateTime.UtcNow;

            if (!IsDepositIntent(intent) && nextStatus == PaymentStatus.Succeeded)
            {
                var reservation = await _dbContext.Reservations
                    .FirstOrDefaultAsync(x => x.Id == intent.ReservationId, cancellationToken);

                if (reservation != null)
                {
                    reservation.Status = ReservationStatus.Paid;
                    reservation.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AdminPaymentStatusApiDto
        {
            PaymentIntentId = intent.Id,
            ReservationId = intent.ReservationId,
            PaymentKind = GetPaymentKind(intent),
            InternalStatus = intent.Status.ToString(),
            ProviderStatus = providerStatus.ToString(),
            Provider = intent.Provider,
            Amount = intent.Amount,
            Currency = _paymentOptions.Currency,
            UpdatedAt = intent.UpdatedAt
        };
    }

    public async Task<int> ProcessPendingWebhookJobsAsync(CancellationToken cancellationToken = default)
    {
        var pendingJobs = await _dbContext.BackgroundJobs
            .Where(x => x.Type == WebhookProcessingJobType
                && x.Status == BackgroundJobStatus.Pending
                && x.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(x => x.ScheduledAt)
            .Take(Math.Max(_paymentOptions.WebhookJobBatchSize, 1))
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var job in pendingJobs)
        {
            try
            {
                job.Status = BackgroundJobStatus.Processing;
                job.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                var payload = JsonSerializer.Deserialize<QueuedWebhookPayload>(
                    job.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Webhook job payload çözümlenemedi.");

                var isMatched = await ApplyWebhookEventAsync(
                    new ParsedWebhookEvent
                    {
                        ProviderEventId = payload.ProviderEventId,
                        EventType = payload.EventType,
                        ProviderIntentId = payload.ProviderIntentId,
                        ProviderTransactionId = payload.ProviderTransactionId,
                        RawPayload = payload.RawPayload
                    },
                    cancellationToken);

                if (!isMatched)
                {
                    throw new InvalidOperationException($"Webhook event {payload.ProviderEventId} could not be matched to any payment intent.");
                }

                var webhookEvent = await _dbContext.PaymentWebhookEvents
                    .FirstOrDefaultAsync(x => x.ProviderEventId == payload.ProviderEventId, cancellationToken);
                if (webhookEvent != null)
                {
                    webhookEvent.Processed = true;
                    webhookEvent.UpdatedAt = DateTime.UtcNow;
                }

                job.Status = BackgroundJobStatus.Completed;
                job.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.UpdatedAt = DateTime.UtcNow;
                if (job.RetryCount >= Math.Max(_paymentOptions.RetryLimit, 1))
                {
                    job.Status = BackgroundJobStatus.Failed;
                }
                else
                {
                    job.Status = BackgroundJobStatus.Pending;
                    job.ScheduledAt = DateTime.UtcNow.AddSeconds(15 * job.RetryCount);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Queued webhook job {BackgroundJobId} failed.", job.Id);
            }
        }

        return processedCount;
    }

    private void EnsureOnlinePaymentEnabled()
    {
        var paymentFeatureFlag = _dbContext.FeatureFlags
            .AsNoTracking()
            .FirstOrDefault(x => x.Name == "EnableOnlinePayment");

        if (paymentFeatureFlag is { Enabled: false })
        {
            throw new InvalidOperationException("Online ödeme şu anda aktif değil.");
        }
    }

    private async Task EnsureRetryLimitAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var retryLimit = Math.Max(_paymentOptions.RetryLimit, 1);
        var attemptCount = await _dbContext.PaymentIntents
            .CountAsync(
                x => x.ReservationId == reservationId
                    && !x.Provider.EndsWith(DepositProviderSuffix),
                cancellationToken);

        if (attemptCount >= retryLimit)
        {
            throw new InvalidOperationException($"Maksimum ödeme deneme sayısı aşıldı ({retryLimit}).");
        }
    }

    private async Task<PaymentIntent?> EnsureDepositPreAuthorizationAsync(
        Reservation reservation,
        string? referenceTransactionId,
        CancellationToken cancellationToken)
    {
        var existingIntent = await GetLatestDepositIntentAsync(reservation.Id, cancellationToken);
        if (existingIntent != null && existingIntent.Status != PaymentStatus.Failed)
        {
            return existingIntent;
        }

        var depositAmount = await GetReservationDepositAmountAsync(reservation.Id, cancellationToken);
        if (depositAmount <= 0)
        {
            return null;
        }

        var providerResult = await ExecuteWithTimeoutRetryAsync(
            () => _paymentProvider.CreatePreAuthorizationAsync(
                new CreatePreAuthorizationProviderRequest
                {
                    ReservationId = reservation.Id,
                    Amount = depositAmount,
                    Currency = _paymentOptions.Currency,
                    ReferenceTransactionId = referenceTransactionId ?? $"reservation-{reservation.Id:N}",
                    IdempotencyKey = $"deposit-{reservation.Id:N}"
                },
                cancellationToken),
            $"deposit-preauth:{reservation.Id}",
            cancellationToken);

        var depositIntent = existingIntent ?? new PaymentIntent
        {
            ReservationId = reservation.Id,
            Amount = depositAmount,
            IdempotencyKey = $"deposit-{reservation.Id:N}",
            Provider = $"{_paymentOptions.Provider}{DepositProviderSuffix}"
        };

        depositIntent.Amount = depositAmount;
        depositIntent.ProviderIntentId = providerResult.ProviderIntentId;
        depositIntent.ProviderTransactionId = providerResult.ProviderTransactionId;
        depositIntent.Status = MapToEntityStatus(providerResult.Status);
        depositIntent.UpdatedAt = DateTime.UtcNow;

        if (existingIntent == null)
        {
            await _dbContext.PaymentIntents.AddAsync(depositIntent, cancellationToken);
        }

        return depositIntent;
    }

    private async Task<bool> ApplyWebhookEventAsync(ParsedWebhookEvent parsedEvent, CancellationToken cancellationToken)
    {
        var intent = await ResolvePaymentIntentAsync(parsedEvent, cancellationToken);
        if (intent == null)
        {
            _logger.LogWarning(
                "Webhook event {EventId} could not be matched to any payment intent. ProviderIntentId={ProviderIntentId}, ProviderTransactionId={ProviderTransactionId}",
                parsedEvent.ProviderEventId,
                parsedEvent.ProviderIntentId,
                parsedEvent.ProviderTransactionId);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(parsedEvent.ProviderTransactionId))
        {
            intent.ProviderTransactionId = parsedEvent.ProviderTransactionId;
        }

        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(x => x.Id == intent.ReservationId, cancellationToken);

        if (parsedEvent.EventType.Contains("authorized", StringComparison.OrdinalIgnoreCase))
        {
            intent.Status = PaymentStatus.Authorized;
        }
        else if (parsedEvent.EventType.Contains("succeeded", StringComparison.OrdinalIgnoreCase))
        {
            intent.Status = PaymentStatus.Succeeded;
            if (reservation != null && !IsDepositIntent(intent))
            {
                reservation.Status = ReservationStatus.Paid;
                reservation.UpdatedAt = DateTime.UtcNow;
                await EnsureDepositPreAuthorizationAsync(reservation, GetPreferredProviderReference(intent), cancellationToken);
            }
        }
        else if (parsedEvent.EventType.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            intent.Status = PaymentStatus.Failed;
        }
        else if (parsedEvent.EventType.Contains("released", StringComparison.OrdinalIgnoreCase))
        {
            intent.Status = PaymentStatus.Cancelled;
        }

        intent.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private async Task<decimal> GetReservationDepositAmountAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation == null)
        {
            return 0m;
        }

        var vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reservation.VehicleId, cancellationToken);
        if (vehicle == null)
        {
            return 0m;
        }

        var vehicleGroup = await _dbContext.VehicleGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vehicle.GroupId, cancellationToken);

        return vehicleGroup?.DepositAmount ?? 0m;
    }

    private async Task<PaymentIntent?> GetLatestRentalPaymentIntentAsync(Guid reservationId, CancellationToken cancellationToken) =>
        await _dbContext.PaymentIntents
            .Where(
                x => x.ReservationId == reservationId
                    && !x.Provider.EndsWith(DepositProviderSuffix))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<PaymentIntent?> GetLatestDepositIntentAsync(Guid reservationId, CancellationToken cancellationToken) =>
        await _dbContext.PaymentIntents
            .Where(
                x => x.ReservationId == reservationId
                    && x.Provider.EndsWith(DepositProviderSuffix))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static decimal CalculateRefundAmount(Reservation reservation, decimal totalAmount)
    {
        var timeUntilPickup = reservation.PickupDateTime - DateTime.UtcNow;
        if (timeUntilPickup <= TimeSpan.Zero)
        {
            return 0m;
        }

        if (timeUntilPickup <= TimeSpan.FromHours(24))
        {
            return Math.Round(totalAmount * 0.80m, 2, MidpointRounding.AwayFromZero);
        }

        return totalAmount;
    }

    private static BackgroundJob CreateWebhookProcessingJob(ParsedWebhookEvent parsedEvent)
    {
        return new BackgroundJob
        {
            Type = WebhookProcessingJobType,
            Payload = JsonSerializer.Serialize(new QueuedWebhookPayload
            {
                ProviderEventId = parsedEvent.ProviderEventId,
                EventType = parsedEvent.EventType,
                ProviderIntentId = parsedEvent.ProviderIntentId,
                ProviderTransactionId = parsedEvent.ProviderTransactionId,
                RawPayload = parsedEvent.RawPayload
            }),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = DateTime.UtcNow
        };
    }

    private async Task<T> ExecuteWithTimeoutRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(_paymentOptions.TimeoutRetryCount, 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation();
            }
            catch (TimeoutException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Payment operation {OperationName} timed out on attempt {Attempt}. Retrying.",
                    operationName,
                    attempt);
            }
        }

        throw new InvalidOperationException($"Ödeme sağlayıcısına erişilemiyor: {operationName}");
    }

    private PaymentIntentApiDto MapIntentToDto(PaymentIntent intent, string reservationStatus) =>
        new()
        {
            PaymentIntentId = intent.Id,
            PaymentKind = GetPaymentKind(intent),
            Status = MapToApiStatus(intent.Status),
            Amount = intent.Amount,
            Currency = _paymentOptions.Currency,
            ExpiresAt = intent.UpdatedAt.AddMinutes(Math.Max(_paymentOptions.IntentExpiresMinutes, 1)),
            TransactionId = intent.ProviderTransactionId ?? intent.ProviderIntentId,
            ReservationStatus = reservationStatus
        };

    private PaymentOperationApiDto BuildSkippedDepositOperation(Guid reservationId, string operation, string? reason) =>
        new()
        {
            ReservationId = reservationId,
            PaymentIntentId = Guid.Empty,
            PaymentKind = "DepositPreAuthorization",
            Operation = operation,
            Status = "Skipped",
            Amount = 0,
            Currency = _paymentOptions.Currency,
            ReferenceId = null,
            Reason = reason
        };

    private static PaymentStatus MapToEntityStatus(PaymentProviderIntentStatus providerStatus) => providerStatus switch
    {
        PaymentProviderIntentStatus.Pending3DS => PaymentStatus.Pending,
        PaymentProviderIntentStatus.Pending => PaymentStatus.Pending,
        PaymentProviderIntentStatus.Authorized => PaymentStatus.Authorized,
        PaymentProviderIntentStatus.Succeeded => PaymentStatus.Succeeded,
        PaymentProviderIntentStatus.Failed => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static PaymentStatus MapToEntityStatus(ProviderTransactionStatus providerStatus, PaymentStatus currentStatus) => providerStatus switch
    {
        ProviderTransactionStatus.Pending => PaymentStatus.Pending,
        ProviderTransactionStatus.Authorized => PaymentStatus.Authorized,
        ProviderTransactionStatus.Succeeded => PaymentStatus.Succeeded,
        ProviderTransactionStatus.Failed => PaymentStatus.Failed,
        _ => currentStatus
    };

    private static string MapToApiStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.Created => PaymentProviderIntentStatus.Pending3DS.ToString(),
        PaymentStatus.Pending => PaymentProviderIntentStatus.Pending3DS.ToString(),
        PaymentStatus.Authorized => PaymentProviderIntentStatus.Authorized.ToString(),
        PaymentStatus.Succeeded => PaymentProviderIntentStatus.Succeeded.ToString(),
        PaymentStatus.Failed => PaymentProviderIntentStatus.Failed.ToString(),
        _ => status.ToString()
    };

    private static bool IsDepositIntent(PaymentIntent intent) =>
        intent.Provider.EndsWith(DepositProviderSuffix, StringComparison.OrdinalIgnoreCase);

    private static string GetPaymentKind(PaymentIntent intent) =>
        IsDepositIntent(intent) ? "DepositPreAuthorization" : "RentalPayment";

    private static string GetRequiredProviderIntentId(PaymentIntent intent)
    {
        if (!string.IsNullOrWhiteSpace(intent.ProviderIntentId))
        {
            return intent.ProviderIntentId;
        }

        throw new InvalidOperationException($"Payment intent {intent.Id} için provider intent id bulunamadı.");
    }

    private static string GetPreferredProviderReference(PaymentIntent intent)
    {
        if (!string.IsNullOrWhiteSpace(intent.ProviderTransactionId))
        {
            return intent.ProviderTransactionId;
        }

        return GetRequiredProviderIntentId(intent);
    }

    private async Task<PaymentIntent?> ResolvePaymentIntentAsync(
        ParsedWebhookEvent parsedEvent,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(parsedEvent.ProviderIntentId))
        {
            var intentByProviderIntentId = await _dbContext.PaymentIntents
                .FirstOrDefaultAsync(x => x.ProviderIntentId == parsedEvent.ProviderIntentId, cancellationToken);
            if (intentByProviderIntentId != null)
            {
                return intentByProviderIntentId;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsedEvent.ProviderTransactionId))
        {
            return await _dbContext.PaymentIntents
                .FirstOrDefaultAsync(x => x.ProviderTransactionId == parsedEvent.ProviderTransactionId, cancellationToken);
        }

        return null;
    }

    private sealed record QueuedWebhookPayload
    {
        public string ProviderEventId { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public string? ProviderIntentId { get; init; }
        public string? ProviderTransactionId { get; init; }
        public string RawPayload { get; init; } = string.Empty;
    }
}


