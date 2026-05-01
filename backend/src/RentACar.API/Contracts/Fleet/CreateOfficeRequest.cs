namespace RentACar.API.Contracts.Fleet;

public sealed record CreateOfficeRequest(
    string Code,
    string Name,
    string Address,
    string Phone,
    bool IsAirport,
    string OpeningHours);
