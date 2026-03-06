namespace RentACar.API.Contracts.Fleet;

public sealed record VehicleGroupDto(
    Guid Id,
    string NameTr,
    string NameEn,
    string NameRu,
    string NameAr,
    string NameDe,
    decimal DepositAmount,
    int MinAge,
    int MinLicenseYears,
    IReadOnlyList<string> Features);
