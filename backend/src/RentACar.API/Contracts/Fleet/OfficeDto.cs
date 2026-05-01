namespace RentACar.API.Contracts.Fleet;

public sealed record OfficeDto(
    Guid Id,
    string Code,
    string Name,
    string Address,
    string Phone,
    bool IsAirport,
    string OpeningHours);
