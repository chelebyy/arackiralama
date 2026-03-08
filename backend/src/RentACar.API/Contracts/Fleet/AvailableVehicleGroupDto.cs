namespace RentACar.API.Contracts.Fleet;

public sealed record AvailableVehicleGroupDto(
    Guid GroupId,
    string GroupName,
    string GroupNameEn,
    int AvailableCount,
    decimal DailyPrice,
    string Currency,
    decimal DepositAmount,
    int MinAge,
    int MinLicenseYears,
    IReadOnlyList<string> Features,
    string? ImageUrl);