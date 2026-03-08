namespace RentACar.API.Contracts.Pricing;

public sealed record UpdateCampaignRequest(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    int MinDays,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    bool IsActive,
    IReadOnlyList<Guid>? AllowedVehicleGroupIds);
