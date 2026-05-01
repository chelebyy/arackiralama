using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Payments;

namespace RentACar.Infrastructure.Services.Payments;

public sealed class IyzicoPaymentProvider(
    IOptions<PaymentOptions> paymentOptions,
    ILogger<IyzicoPaymentProvider> logger) : IPaymentProvider
{
    private readonly PaymentOptions _options = paymentOptions.Value;
    private readonly ILogger<IyzicoPaymentProvider> _logger = logger;

    public Task<PaymentIntentProviderResult> CreatePaymentIntentAsync(
        CreatePaymentIntentProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerIntentId = Guid.NewGuid().ToString("N");
        var callbackBase = string.IsNullOrWhiteSpace(_options.Iyzico.BaseUrl)
            ? "https://sandbox-api.iyzipay.com"
            : _options.Iyzico.BaseUrl.TrimEnd('/');

        var result = new PaymentIntentProviderResult
        {
            ProviderIntentId = providerIntentId,
            Status = PaymentProviderIntentStatus.Pending3DS,
            RedirectUrl = $"{callbackBase}/mock-3ds?token={providerIntentId}",
            ProviderTransactionId = $"iyzico-tx-{Guid.NewGuid():N}",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(_options.IntentExpiresMinutes, 1))
        };

        _logger.LogInformation(
            "Iyzico intent prepared for reservation {ReservationId} (sandbox simulation).",
            request.ReservationId);

        return Task.FromResult(result);
    }

    public Task<PreAuthorizationProviderResult> CreatePreAuthorizationAsync(
        CreatePreAuthorizationProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return Task.FromResult(new PreAuthorizationProviderResult
            {
                Status = PaymentProviderIntentStatus.Failed,
                FailureCode = "IYZICO_INVALID_DEPOSIT_AMOUNT",
                FailureMessage = "Deposit amount must be greater than zero.",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(_options.IntentExpiresMinutes, 1))
            });
        }

        return Task.FromResult(new PreAuthorizationProviderResult
        {
            ProviderIntentId = $"iyzico-preauth-{Guid.NewGuid():N}",
            ProviderTransactionId = $"iyzico-preauth-tx-{Guid.NewGuid():N}",
            Status = PaymentProviderIntentStatus.Authorized,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(_options.IntentExpiresMinutes, 1))
        });
    }

    public Task<PaymentVerificationProviderResult> VerifyPaymentAsync(
        PaymentCallbackProviderRequest callback,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentVerificationProviderResult
        {
            Status = PaymentProviderIntentStatus.Succeeded,
            TransactionId = $"iyzico-tx-{Guid.NewGuid():N}"
        });
    }

    public bool VerifyWebhookSignature(string payload, string signature, string? timestamp)
    {
        if (!TryGetWebhookTimestampUtc(timestamp, out var timestampUtc))
        {
            return false;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (nowUtc - timestampUtc > TimeSpan.FromSeconds(300))
        {
            return false;
        }

        if (timestampUtc - nowUtc > TimeSpan.FromSeconds(30))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(_options.Iyzico.WebhookSecret))
        {
            return false;
        }

        return PaymentSignatureHelper.IsValidSignature(payload, _options.Iyzico.WebhookSecret, signature);
    }

    public Task<ParsedWebhookEvent> ParseWebhookAsync(
        string provider,
        string payload,
        string? eventType,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var providerEventId = TryGetString(root, "eventId")
            ?? TryGetString(root, "provider_event_id")
            ?? Guid.NewGuid().ToString("N");

        var providerIntentId = TryGetString(root, "paymentConversationId")
            ?? TryGetString(root, "payment_intent_id");
        var providerTransactionId = TryGetString(root, "paymentId")
            ?? TryGetString(root, "provider_transaction_id")
            ?? TryGetString(root, "transaction_id");

        var detectedEventType = eventType
            ?? TryGetString(root, "eventType")
            ?? TryGetString(root, "event_type")
            ?? "unknown";

        return Task.FromResult(new ParsedWebhookEvent
        {
            ProviderEventId = providerEventId,
            EventType = detectedEventType,
            ProviderIntentId = providerIntentId,
            ProviderTransactionId = providerTransactionId,
            RawPayload = payload
        });
    }

    public Task<ProviderTransactionStatus> GetTransactionStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return Task.FromResult(ProviderTransactionStatus.Unknown);
        }

        return Task.FromResult(ProviderTransactionStatus.Unknown);
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
                FailureCode = "IYZICO_INVALID_AMOUNT",
                FailureMessage = "Refund amount must be greater than zero."
            });
        }

        return Task.FromResult(new ProviderRefundResult
        {
            Success = true,
            ReferenceId = $"iyzico-refund-{Guid.NewGuid():N}"
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
                FailureCode = "IYZICO_RELEASE_INVALID_INTENT",
                FailureMessage = "Provider intent id is required."
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
                FailureCode = "IYZICO_CAPTURE_INVALID_INTENT",
                FailureMessage = "Provider intent id is required."
            });
        }

        if (request.Amount <= 0)
        {
            return Task.FromResult(new ProviderCaptureDepositResult
            {
                Success = false,
                FailureCode = "IYZICO_CAPTURE_INVALID_AMOUNT",
                FailureMessage = "Capture amount must be greater than zero."
            });
        }

        return Task.FromResult(new ProviderCaptureDepositResult
        {
            Success = true,
            ReferenceId = $"iyzico-capture-{Guid.NewGuid():N}"
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

    private static bool TryGetWebhookTimestampUtc(string? timestamp, out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return false;
        }

        if (long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            try
            {
                timestampUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        return DateTimeOffset.TryParse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces,
            out timestampUtc);
    }
}
