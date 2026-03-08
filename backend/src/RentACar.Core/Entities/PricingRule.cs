namespace RentACar.Core.Entities;

public class PricingRule : BaseEntity
{
    public Guid VehicleGroupId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal DailyPrice { get; set; }
    public decimal Multiplier { get; set; } = 1m;
    public decimal WeekdayMultiplier { get; set; } = 1m;
    public decimal WeekendMultiplier { get; set; } = 1m;
    public string CalculationType { get; set; } = "multiplier";
    public int Priority { get; set; }

    public VehicleGroup? VehicleGroup { get; set; }
}
