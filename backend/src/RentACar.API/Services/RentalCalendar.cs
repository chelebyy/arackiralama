namespace RentACar.API.Services;

internal static class RentalCalendar
{
    public static DateOnly TurkeyDate(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(3));

    public static int RentalDays(DateTime pickupUtc, DateTime returnUtc) =>
        Math.Max(1, TurkeyDate(returnUtc).DayNumber - TurkeyDate(pickupUtc).DayNumber);
}
