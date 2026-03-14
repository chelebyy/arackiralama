namespace RentACar.API.Contracts.Payments;

public record PaymentIntentApiDto
{
    public Guid PaymentIntentId { get; init; }
    public string PaymentKind { get; init; } = "RentalPayment";
    public string Status { get; init; } = string.Empty;
    public string? RedirectUrl { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public DateTime ExpiresAt { get; init; }
    public string? TransactionId { get; init; }
    public string ReservationStatus { get; init; } = string.Empty;
}

public record WebhookProcessApiDto
{
    public string ProviderEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public bool Duplicate { get; init; }
    public bool Processed { get; init; }
}

public record PaymentOperationApiDto
{
    public Guid ReservationId { get; init; }
    public Guid PaymentIntentId { get; init; }
    public string PaymentKind { get; init; } = "RentalPayment";
    public string Operation { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string? ReferenceId { get; init; }
    public string? Reason { get; init; }
}

public record AdminPaymentStatusApiDto
{
    public Guid PaymentIntentId { get; init; }
    public Guid ReservationId { get; init; }
    public string PaymentKind { get; init; } = "RentalPayment";
    public string InternalStatus { get; init; } = string.Empty;
    public string ProviderStatus { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public DateTime UpdatedAt { get; init; }
}
