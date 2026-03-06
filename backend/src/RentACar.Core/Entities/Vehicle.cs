using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class Vehicle : BaseEntity
{
    public string Plate { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Color { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public Guid OfficeId { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    public string? PhotoUrl { get; set; }

    public VehicleGroup? Group { get; set; }
    public Office? Office { get; set; }
}
