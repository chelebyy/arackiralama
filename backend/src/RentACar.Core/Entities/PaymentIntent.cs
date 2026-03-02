namespace RentACar.Core.Entities;

public class PaymentIntent : BaseEntity
{
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Created";
    public string Provider { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    public Reservation? Reservation { get; set; }
}
