namespace RentACar.API.Contracts.Payments;

public record CreatePaymentIntentApiRequest
{
    public Guid ReservationId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public int InstallmentCount { get; init; } = 1;
    public PaymentCardApiRequest Card { get; init; } = new();
}

public record PaymentCardApiRequest
{
    public string HolderName { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string ExpiryMonth { get; init; } = string.Empty;
    public string ExpiryYear { get; init; } = string.Empty;
    public string Cvv { get; init; } = string.Empty;
}

public record ThreeDsReturnApiRequest
{
    public string BankResponse { get; init; } = string.Empty;
}

public record AdminRefundApiRequest
{
    public decimal? Amount { get; init; }
    public string? Reason { get; init; }
    public string? IdempotencyKey { get; init; }
}

public record AdminReleaseDepositApiRequest
{
    public string? Note { get; init; }
}

public record AdminPaymentRetryApiRequest
{
    public Guid ReservationId { get; init; }
    public string? IdempotencyKey { get; init; }
    public int InstallmentCount { get; init; } = 1;
    public PaymentCardApiRequest Card { get; init; } = new();
}
