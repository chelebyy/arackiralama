namespace RentACar.API.Contracts.Pricing;

public sealed record PricingRuleDto(
    Guid Id,
    Guid VehicleGroupId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal DailyPrice,
    decimal Multiplier,
    decimal WeekdayMultiplier,
    decimal WeekendMultiplier,
    string CalculationType,
    int Priority);
