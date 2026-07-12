namespace RentACar.Core.Entities;

public sealed class ReservationQuoteV1
{
    public Guid QuoteId { get; set; }
    public string SessionHash { get; set; } = string.Empty;
    public Guid VehicleGroupId { get; set; }
    public Guid PickupOfficeId { get; set; }
    public Guid ReturnOfficeId { get; set; }
    public DateTime PickupDateTimeUtc { get; set; }
    public DateTime ReturnDateTimeUtc { get; set; }
    public string? CampaignCode { get; set; }
    public int? DriverAge { get; set; }
    public bool FullCoverageWaiver { get; set; }
    public string Locale { get; set; } = "tr";
    public List<ReservationQuotedExtraV1> SelectedExtras { get; set; } = new();
    public ReservationPricingSnapshotV1 PricingSnapshot { get; set; } = new();
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class ReservationQuotedExtraV1
{
    public Guid ExtraOptionId { get; set; }
    public uint OptionVersion { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Locale { get; set; } = "tr";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string PricingMode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int RentalDays { get; set; }
    public decimal Total { get; set; }
}
