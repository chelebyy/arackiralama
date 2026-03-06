namespace RentACar.API.Contracts.Fleet;

public sealed record OfficeDto(
    Guid Id,
    string Name,
    string Address,
    string Phone,
    bool IsAirport,
    string OpeningHours);
