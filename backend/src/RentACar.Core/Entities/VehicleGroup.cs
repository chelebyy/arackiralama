namespace RentACar.Core.Entities;

public class VehicleGroup : BaseEntity
{
    public string NameTr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameDe { get; set; } = string.Empty;
    public decimal DepositAmount { get; set; }
    public int MinAge { get; set; }
    public int MinLicenseYears { get; set; }
    public List<string> Features { get; set; } = new();

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<PricingRule> PricingRules { get; set; } = new List<PricingRule>();
}
