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
    public string? Transmission { get; set; }
    public string? FuelType { get; set; }
    public int? SeatCount { get; set; }
    public int? LuggageCapacity { get; set; }
    public string? BodyType { get; set; }
    public int? DoorCount { get; set; }
    public string? Engine { get; set; }
    public int? PowerHp { get; set; }
    public string[] Equipment { get; set; } = [];
    public string[] PhotoUrls { get; set; } = [];

    public VehicleGroup? Group { get; set; }
    public Office? Office { get; set; }
}
