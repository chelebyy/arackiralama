namespace RentACar.Core.Interfaces.Payments;

public enum PaymentProviderIntentStatus
{
    Pending3DS = 0,
    Pending = 1,
    Authorized = 2,
    Succeeded = 3,
    Failed = 4
}

public enum ProviderTransactionStatus
{
    Unknown = 0,
    Pending = 1,
    Authorized = 2,
    Succeeded = 3,
    Failed = 4
}

public record ProviderCardData
{
    public string HolderName { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string ExpiryMonth { get; init; } = string.Empty;
    public string ExpiryYear { get; init; } = string.Empty;
    public string Cvv { get; init; } = string.Empty;
}

public record CreatePaymentIntentProviderRequest
{
    public Guid ReservationId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string IdempotencyKey { get; init; } = string.Empty;
    public int InstallmentCount { get; init; } = 1;
    public ProviderCardData Card { get; init; } = new();
}

public record PaymentIntentProviderResult
{
    public string ProviderIntentId { get; init; } = string.Empty;
    public PaymentProviderIntentStatus Status { get; init; } = PaymentProviderIntentStatus.Pending3DS;
    public string? RedirectUrl { get; init; }
    public string? ProviderTransactionId { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}

public record CreatePreAuthorizationProviderRequest
{
    public Guid ReservationId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string ReferenceTransactionId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
}

public record PreAuthorizationProviderResult
{
    public string ProviderIntentId { get; init; } = string.Empty;
    public PaymentProviderIntentStatus Status { get; init; } = PaymentProviderIntentStatus.Pending;
    public string? ProviderTransactionId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}

public record PaymentCallbackProviderRequest
{
    public string ProviderIntentId { get; init; } = string.Empty;
    public string BankResponse { get; init; } = string.Empty;
    public string RawPayload { get; init; } = string.Empty;
}

public record PaymentVerificationProviderResult
{
    public PaymentProviderIntentStatus Status { get; init; } = PaymentProviderIntentStatus.Pending;
    public string? TransactionId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
}

public record ParsedWebhookEvent
{
    public string ProviderEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string? ProviderIntentId { get; init; }
    public string? ProviderTransactionId { get; init; }
    public string RawPayload { get; init; } = string.Empty;
}

public record ProviderRefundRequest
{
    public string ProviderIntentId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string? Reason { get; init; }
}

public record ProviderRefundResult
{
    public bool Success { get; init; }
    public string? ReferenceId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
}

public record ProviderReleaseDepositRequest
{
    public string ProviderIntentId { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public record ProviderReleaseDepositResult
{
    public bool Success { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
}

public record ProviderCaptureDepositRequest
{
    public string ProviderIntentId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string? Note { get; init; }
}

public record ProviderCaptureDepositResult
{
    public bool Success { get; init; }
    public string? ReferenceId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
}

