using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class ReservationExtraOption : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public ReservationExtraPricingMode PricingMode { get; set; }
    public int MaxQuantity { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public uint Version { get; set; }

    public ICollection<ReservationExtraOptionTranslation> Translations { get; set; } = new List<ReservationExtraOptionTranslation>();
    public ICollection<ReservationExtraOptionVehicleGroup> VehicleGroups { get; set; } = new List<ReservationExtraOptionVehicleGroup>();
    public ICollection<ReservationSelectedExtra> SelectedExtras { get; set; } = new List<ReservationSelectedExtra>();
}
