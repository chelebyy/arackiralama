using RentACar.Core.Enums;

namespace RentACar.API.Contracts.Fleet;

public sealed record CreateVehicleRequest(
    string Plate,
    string Brand,
    string Model,
    int Year,
    string Color,
    Guid GroupId,
    Guid OfficeId,
    VehicleStatus Status);
