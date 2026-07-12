namespace RentACar.Core.Entities;

public class ReservationPricingSnapshotV1
{
    public int SchemaVersion { get; set; } = 1;
    public decimal DailyRate { get; set; }
    public int RentalDays { get; set; }
    public decimal BaseTotal { get; set; }
    public decimal AirportFee { get; set; }
    public decimal OneWayFee { get; set; }
    public decimal YoungDriverFee { get; set; }
    public decimal CoverageWaiverFee { get; set; }
    public decimal OtherFees { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignCode { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountTotal { get; set; }
    public List<ReservationPricingExtraSnapshot> ExtraItems { get; set; } = new();
    public decimal ExtrasTotal { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal PreAuthorizationAmount { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal FinalTotal { get; set; }
    public Guid QuoteId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public class ReservationPricingExtraSnapshot
{
    public Guid ExtraOptionId { get; set; }
    public uint OptionVersion { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string PricingMode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int RentalDays { get; set; }
    public decimal Total { get; set; }
}
