using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Payments;

namespace RentACar.Infrastructure.Services.Payments;

public sealed class MockPaymentProvider(
    IOptions<PaymentOptions> paymentOptions,
    ILogger<MockPaymentProvider> logger) : IPaymentProvider
{
    private readonly PaymentOptions _options = paymentOptions.Value;
    private readonly ILogger<MockPaymentProvider> _logger = logger;

    public Task<PaymentIntentProviderResult> CreatePaymentIntentAsync(
        CreatePaymentIntentProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.IdempotencyKey.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || request.Card.HolderName.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException("Mock provider simulated timeout while creating payment intent.");
        }

        var providerIntentId = Guid.NewGuid().ToString("N");
        var providerTransactionId = $"mock-tx-{Guid.NewGuid():N}";

        var result = new PaymentIntentProviderResult
        {
            ProviderIntentId = providerIntentId,
            Status = PaymentProviderIntentStatus.Pending3DS,
            RedirectUrl = $"https://mock-payment.local/3ds?intent={providerIntentId}",
            ProviderTransactionId = providerTransactionId,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(_options.IntentExpiresMinutes, 1))
        };

        _logger.LogInformation(
            "Mock payment intent created for reservation {ReservationId} with idempotency {IdempotencyKey}",
            request.ReservationId,
            request.IdempotencyKey);

        return Task.FromResult(result);
    }

    public Task<PreAuthorizationProviderResult> CreatePreAuthorizationAsync(
        CreatePreAuthorizationProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ReferenceTransactionId.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException("Mock provider simulated timeout while creating deposit pre-authorization.");
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(new PreAuthorizationProviderResult
            {
                Status = PaymentProviderIntentStatus.Failed,
                FailureCode = "MOCK_INVALID_DEPOSIT_AMOUNT",
                FailureMessage = "Deposit amount must be greater than zero.",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(_options.IntentExpiresMinutes, 1))
            });
        }

        return Task.FromResult(new PreAuthorizationProviderResult
        {
            ProviderIntentId = $"mock-deposit-{Guid.NewGuid():N}",
            ProviderTransactionId = $"mock-deposit-tx-{Guid.NewGuid():N}",
            Status = PaymentProviderIntentStatus.Authorized,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(_options.IntentExpiresMinutes, 1))
        });
    }

    public Task<PaymentVerificationProviderResult> VerifyPaymentAsync(
        PaymentCallbackProviderRequest callback,
        CancellationToken cancellationToken = default)
    {
        if (callback.BankResponse.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException("Mock provider simulated timeout while verifying payment.");
        }

        var isFailure = callback.BankResponse.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || callback.BankResponse.Contains("cancel", StringComparison.OrdinalIgnoreCase);

        if (isFailure)
        {
            return Task.FromResult(new PaymentVerificationProviderResult
            {
                Status = PaymentProviderIntentStatus.Failed,
                FailureCode = "MOCK_3DS_FAILED",
                FailureMessage = "Mock provider marked payment as failed."
            });
        }

        return Task.FromResult(new PaymentVerificationProviderResult
        {
            Status = PaymentProviderIntentStatus.Succeeded,
            TransactionId = $"mock-tx-{Guid.NewGuid():N}"
        });
    }

    public bool VerifyWebhookSignature(string payload, string signature, string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        return PaymentSignatureHelper.IsValidSignature(payload, _options.Mock.WebhookSecret, signature);
    }

    public Task<ParsedWebhookEvent> ParseWebhookAsync(
        string provider,
        string payload,
        string? eventType,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var providerEventId = TryGetString(root, "provider_event_id")
            ?? TryGetString(root, "event_id")
            ?? Guid.NewGuid().ToString("N");

        var intentId = TryGetString(root, "payment_intent_id")
            ?? TryGetString(root, "intent_id");
        var transactionId = TryGetString(root, "provider_transaction_id")
            ?? TryGetString(root, "transaction_id");

        return Task.FromResult(new ParsedWebhookEvent
        {
            ProviderEventId = providerEventId,
            EventType = eventType ?? TryGetString(root, "event_type") ?? "unknown",
            ProviderIntentId = intentId,
            ProviderTransactionId = transactionId,
            RawPayload = payload
        });
    }

    public Task<ProviderTransactionStatus> GetTransactionStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        if (transactionId.Contains("deposit", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ProviderTransactionStatus.Authorized);
        }

        var status = transactionId.Contains("fail", StringComparison.OrdinalIgnoreCase)
            ? ProviderTransactionStatus.Failed
            : ProviderTransactionStatus.Succeeded;
        return Task.FromResult(status);
    }

    public Task<ProviderRefundResult> RefundAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return Task.FromResult(new ProviderRefundResult
            {
                Success = false,
                FailureCode = "MOCK_INVALID_AMOUNT",
                FailureMessage = "Refund amount must be greater than zero."
            });
        }

        if (request.Reason?.Contains("fail", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Task.FromResult(new ProviderRefundResult
            {
                Success = false,
                FailureCode = "MOCK_REFUND_FAILED",
                FailureMessage = "Mock provider forced refund failure."
            });
        }

        return Task.FromResult(new ProviderRefundResult
        {
            Success = true,
            ReferenceId = $"mock-refund-{Guid.NewGuid():N}"
        });
    }

    public Task<ProviderReleaseDepositResult> ReleaseDepositAsync(
        ProviderReleaseDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderIntentId))
        {
            return Task.FromResult(new ProviderReleaseDepositResult
            {
                Success = false,
                FailureCode = "MOCK_RELEASE_INVALID_INTENT",
                FailureMessage = "Provider intent id is required."
            });
        }

        if (request.ProviderIntentId.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProviderReleaseDepositResult
            {
                Success = false,
                FailureCode = "MOCK_RELEASE_FAILED",
                FailureMessage = "Mock provider forced release failure."
            });
        }

        return Task.FromResult(new ProviderReleaseDepositResult
        {
            Success = true
        });
    }

    public Task<ProviderCaptureDepositResult> CaptureDepositAsync(
        ProviderCaptureDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderIntentId))
        {
            return Task.FromResult(new ProviderCaptureDepositResult
            {
                Success = false,
                FailureCode = "MOCK_CAPTURE_INVALID_INTENT",
                FailureMessage = "Provider intent id is required."
            });
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(new ProviderCaptureDepositResult
            {
                Success = false,
                FailureCode = "MOCK_CAPTURE_INVALID_AMOUNT",
                FailureMessage = "Capture amount must be greater than zero."
            });
        }

        if (request.ProviderIntentId.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProviderCaptureDepositResult
            {
                Success = false,
                FailureCode = "MOCK_CAPTURE_FAILED",
                FailureMessage = "Mock provider forced capture failure."
            });
        }

        return Task.FromResult(new ProviderCaptureDepositResult
        {
            Success = true,
            ReferenceId = $"mock-capture-{Guid.NewGuid():N}"
        });
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }
}

