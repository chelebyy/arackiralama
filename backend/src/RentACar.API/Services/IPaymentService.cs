using RentACar.API.Contracts.Payments;

namespace RentACar.API.Services;

public interface IPaymentService
{
    Task<PaymentIntentApiDto?> CreateIntentAsync(
        CreatePaymentIntentApiRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentApiDto?> CompleteThreeDsAsync(
        Guid intentId,
        ThreeDsReturnApiRequest request,
        CancellationToken cancellationToken = default);

    Task<WebhookProcessApiDto> ProcessWebhookAsync(
        string provider,
        string payload,
        string signature,
        string? timestamp,
        string? eventType,
        CancellationToken cancellationToken = default);

    Task<PaymentOperationApiDto?> RefundReservationAsync(
        Guid reservationId,
        AdminRefundApiRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentOperationApiDto?> ReleaseDepositAsync(
        Guid reservationId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<PaymentOperationApiDto?> CreateDepositPreAuthorizationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<PaymentOperationApiDto?> CaptureDepositAsync(
        Guid reservationId,
        decimal amount,
        string? note,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentApiDto?> RetryPaymentAsync(
        AdminPaymentRetryApiRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminPaymentStatusApiDto?> GetPaymentStatusAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);
}
