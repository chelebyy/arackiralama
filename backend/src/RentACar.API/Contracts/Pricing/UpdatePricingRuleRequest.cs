namespace RentACar.API.Contracts.Pricing;

public sealed record UpdatePricingRuleRequest(
    Guid VehicleGroupId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DailyPrice,
    decimal Multiplier,
    decimal WeekdayMultiplier,
    decimal WeekendMultiplier,
    string CalculationType,
    int Priority);
