using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class Reservation : BaseEntity
{
    public string PublicCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid PickupOfficeId { get; set; }
    public Guid ReturnOfficeId { get; set; }
    public DateTime PickupDateTime { get; set; }
    public DateTime ReturnDateTime { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Draft;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime? UnpaidRequestExpiresAtUtc { get; set; }
    public string? DriverFirstName { get; set; }
    public string? DriverLastName { get; set; }
    public DateTime? DriverDateOfBirth { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseCountry { get; set; }
    public DateTime? DriverLicenseIssueDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public uint Version { get; set; }

    public Customer? Customer { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Office? PickupOffice { get; set; }
    public Office? ReturnOffice { get; set; }
    public ICollection<PaymentIntent> PaymentIntents { get; set; } = new List<PaymentIntent>();
    public ICollection<ReservationHold> Holds { get; set; } = new List<ReservationHold>();
}
