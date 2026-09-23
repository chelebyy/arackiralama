using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class ReservationSelectedExtra
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public Guid ExtraOptionId { get; set; }
    public uint OptionVersionSnapshot { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string OptionCodeSnapshot { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; set; }
    public ReservationExtraPricingMode PricingModeSnapshot { get; set; }
    public int Quantity { get; set; }
    public int RentalDaysSnapshot { get; set; }
    public decimal TotalPriceSnapshot { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Reservation? Reservation { get; set; }
    public ReservationExtraOption? ExtraOption { get; set; }
}
