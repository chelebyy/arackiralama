namespace RentACar.Core.Entities;

public class FeatureFlag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Description { get; set; } = string.Empty;
}
