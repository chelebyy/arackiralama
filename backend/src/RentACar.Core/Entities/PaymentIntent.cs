using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class PaymentIntent : BaseEntity
{
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Created;
    public string Provider { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ProviderIntentId { get; set; }
    public string? ProviderTransactionId { get; set; }

    public Reservation? Reservation { get; set; }
}
