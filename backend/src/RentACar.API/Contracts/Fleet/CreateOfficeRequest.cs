namespace RentACar.API.Contracts.Fleet;

public sealed record CreateOfficeRequest(
    string Code,
    string Name,
    string Address,
    string Phone,
    bool IsAirport,
    bool IsActive,
    string OpeningHours,
    RentACar.Core.Entities.OfficeOperatingPolicy? OperatingPolicy = null);
