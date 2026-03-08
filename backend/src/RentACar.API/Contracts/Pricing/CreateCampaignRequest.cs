namespace RentACar.API.Contracts.Pricing;

public sealed record CreateCampaignRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int MinDays,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    bool IsActive,
    IReadOnlyList<Guid>? AllowedVehicleGroupIds);
