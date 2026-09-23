using RentACar.Core.Interfaces.Payments;

namespace RentACar.Infrastructure.Services.Payments;

public sealed class DisabledPaymentProvider : IPaymentProvider
{
    private const string FailureCode = "PAYMENTS_DISABLED";
    private const string FailureMessage = "Payment processing is disabled.";

    public Task<PaymentIntentProviderResult> CreatePaymentIntentAsync(
        CreatePaymentIntentProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentIntentProviderResult
        {
            Status = PaymentProviderIntentStatus.Failed
        });
    }

    public Task<PreAuthorizationProviderResult> CreatePreAuthorizationAsync(
        CreatePreAuthorizationProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PreAuthorizationProviderResult
        {
            Status = PaymentProviderIntentStatus.Failed,
            FailureCode = FailureCode,
            FailureMessage = FailureMessage
        });
    }

    public Task<PaymentVerificationProviderResult> VerifyPaymentAsync(
        PaymentCallbackProviderRequest callback,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentVerificationProviderResult
        {
            Status = PaymentProviderIntentStatus.Failed,
            FailureCode = FailureCode,
            FailureMessage = FailureMessage
        });
    }

    public bool VerifyWebhookSignature(string payload, string signature, string? timestamp)
    {
        return false;
    }

    public Task<ParsedWebhookEvent> ParseWebhookAsync(
        string provider,
        string payload,
        string? eventType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromException<ParsedWebhookEvent>(new InvalidOperationException(FailureMessage));
    }

    public Task<ProviderTransactionStatus> GetTransactionStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProviderTransactionStatus.Unknown);
    }

    public Task<ProviderRefundResult> RefundAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderRefundResult
        {
            Success = false,
            FailureCode = FailureCode,
            FailureMessage = FailureMessage
        });
    }

    public Task<ProviderReleaseDepositResult> ReleaseDepositAsync(
        ProviderReleaseDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderReleaseDepositResult
        {
            Success = false,
            FailureCode = FailureCode,
            FailureMessage = FailureMessage
        });
    }

    public Task<ProviderCaptureDepositResult> CaptureDepositAsync(
        ProviderCaptureDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCaptureDepositResult
        {
            Success = false,
            FailureCode = FailureCode,
            FailureMessage = FailureMessage
        });
    }
}
