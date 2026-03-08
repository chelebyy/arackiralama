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
    string? AppliedCampaignCode);
