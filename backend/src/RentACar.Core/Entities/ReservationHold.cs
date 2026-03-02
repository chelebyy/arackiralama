namespace RentACar.Core.Entities;

public class ReservationHold : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string SessionId { get; set; } = string.Empty;

    public Reservation? Reservation { get; set; }
    public Vehicle? Vehicle { get; set; }
}
