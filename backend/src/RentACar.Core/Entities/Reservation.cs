using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class Reservation : BaseEntity
{
    public string PublicCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTime PickupDateTime { get; set; }
    public DateTime ReturnDateTime { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Draft;
    public decimal TotalAmount { get; set; }

    public Customer? Customer { get; set; }
    public Vehicle? Vehicle { get; set; }
    public ICollection<PaymentIntent> PaymentIntents { get; set; } = new List<PaymentIntent>();
    public ICollection<ReservationHold> Holds { get; set; } = new List<ReservationHold>();
}
