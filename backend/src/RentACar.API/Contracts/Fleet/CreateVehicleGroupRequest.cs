namespace RentACar.API.Contracts.Fleet;

public sealed record CreateVehicleGroupRequest(
    string NameTr,
    string NameEn,
    string NameRu,
    string NameAr,
    string NameDe,
    decimal DepositAmount,
    int MinAge,
    int MinLicenseYears,
    IReadOnlyList<string>? Features);
