namespace RentACar.API.Contracts.Pricing;

public sealed record PriceBreakdownDto(
    decimal DailyRate,
    int RentalDays,
    decimal BaseTotal,
    decimal ExtrasTotal,
    decimal CampaignDiscount,
    decimal AirportFee,
    decimal OneWayFee,
    decimal ExtraDriverFee,
    decimal ChildSeatFee,
    decimal YoungDriverFee,
    decimal FullCoverageWaiverFee,
    decimal FinalTotal,
    decimal DepositAmount,
    decimal PreAuthorizationAmount,
    string Currency,
    string? AppliedCampaignCode)
{
    public Guid? AppliedCampaignId { get; init; }
    public string? AppliedCampaignDiscountType { get; init; }
    public decimal? AppliedCampaignDiscountValue { get; init; }
    public IReadOnlyList<ReservationExtraLineItemDto> ExtraItems { get; init; } = [];
}

public sealed record ReservationExtraLineItemDto(
    Guid OptionId,
    uint OptionVersion,
    string Code,
    string Name,
    string Description,
    decimal UnitPrice,
    string PricingMode,
    int Quantity,
    int RentalDays,
    decimal Total);
