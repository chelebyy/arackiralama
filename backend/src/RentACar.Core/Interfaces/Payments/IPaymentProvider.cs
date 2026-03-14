namespace RentACar.Core.Interfaces.Payments;

public interface IPaymentProvider
{
    Task<PaymentIntentProviderResult> CreatePaymentIntentAsync(
        CreatePaymentIntentProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<PreAuthorizationProviderResult> CreatePreAuthorizationAsync(
        CreatePreAuthorizationProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationProviderResult> VerifyPaymentAsync(
        PaymentCallbackProviderRequest callback,
        CancellationToken cancellationToken = default);

    bool VerifyWebhookSignature(string payload, string signature, string? timestamp);

    Task<ParsedWebhookEvent> ParseWebhookAsync(
        string provider,
        string payload,
        string? eventType,
        CancellationToken cancellationToken = default);

    Task<ProviderTransactionStatus> GetTransactionStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default);

    Task<ProviderRefundResult> RefundAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderReleaseDepositResult> ReleaseDepositAsync(
        ProviderReleaseDepositRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderCaptureDepositResult> CaptureDepositAsync(
        ProviderCaptureDepositRequest request,
        CancellationToken cancellationToken = default);
}
