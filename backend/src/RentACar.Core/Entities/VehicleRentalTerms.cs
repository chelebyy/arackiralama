namespace RentACar.Core.Entities;

public sealed class VehicleRentalTerms
{
    public decimal? DepositAmount { get; set; }
    public int? MinAge { get; set; }
    public int? MinLicenseYears { get; set; }
    public List<VehicleRentalRate> Rates { get; set; } = [];
    public Guid[] ExtraOptionIds { get; set; } = [];
}

public sealed class VehicleRentalRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal DailyPrice { get; set; }
    public decimal Multiplier { get; set; } = 1m;
    public decimal WeekdayMultiplier { get; set; } = 1m;
    public decimal WeekendMultiplier { get; set; } = 1m;
    public string CalculationType { get; set; } = "multiplier";
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
