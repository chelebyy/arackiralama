namespace RentACar.API.Contracts.Pricing;

public sealed record SelectedReservationExtraInput
{
    public Guid OptionId { get; init; }
    public int Quantity { get; init; }
    public uint OptionVersion { get; init; }
}

public sealed record CreateReservationQuoteRequest
{
    public Guid VehicleGroupId { get; init; }
    public Guid PickupOfficeId { get; init; }
    public Guid ReturnOfficeId { get; init; }
    public DateTime PickupDateTimeUtc { get; init; }
    public DateTime ReturnDateTimeUtc { get; init; }
    public string? CampaignCode { get; init; }
    public int? DriverAge { get; init; }
    public bool FullCoverageWaiver { get; init; }
    public string Locale { get; init; } = "tr";
    public IReadOnlyList<SelectedReservationExtraInput> SelectedExtras { get; init; } = [];
}

public sealed record ReservationQuoteDto(
    Guid QuoteId,
    DateTime ExpiresAtUtc,
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
    string? AppliedCampaignCode,
    IReadOnlyList<ReservationExtraLineItemDto> ExtraItems);
