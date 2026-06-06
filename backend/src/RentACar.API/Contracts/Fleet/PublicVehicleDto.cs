namespace RentACar.API.Contracts.Fleet;

public sealed record PublicVehicleDto(
    Guid Id,
    string Plate,
    string Brand,
    string Model,
    int Year,
    string Color,
    Guid GroupId,
    string GroupName,
    string GroupNameEn,
    Guid OfficeId,
    string Status,
    string? PhotoUrl,
    decimal DailyPrice,
    decimal DepositAmount,
    int MinAge,
    int MinLicenseYears,
    IReadOnlyList<string> Features);
