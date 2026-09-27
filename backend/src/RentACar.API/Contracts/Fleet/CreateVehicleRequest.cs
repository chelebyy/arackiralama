using RentACar.Core.Enums;

namespace RentACar.API.Contracts.Fleet;

public sealed record CreateVehicleRequest(
    string Plate,
    string Brand,
    string Model,
    int Year,
    string Color,
    Guid? GroupId,
    Guid OfficeId,
    VehicleStatus Status,
    string? Transmission = null,
    string? FuelType = null,
    int? SeatCount = null,
    int? LuggageCapacity = null,
    string? BodyType = null,
    int? DoorCount = null,
    string? Engine = null,
    int? PowerHp = null,
    string[]? Equipment = null,
    RentACar.Core.Entities.VehicleRentalTerms? RentalTerms = null);
