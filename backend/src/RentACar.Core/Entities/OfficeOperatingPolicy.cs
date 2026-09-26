namespace RentACar.Core.Entities;

public sealed class OfficeOperatingPolicy
{
    public int? MinimumNoticeMinutes { get; set; }
    public int? PreparationMinutes { get; set; }
    public List<OfficeOperatingWindow> PickupWindows { get; set; } = [];
    public List<OfficeOperatingWindow> ReturnWindows { get; set; } = [];
    public DateOnly[] ClosedDates { get; set; } = [];
}

public sealed class OfficeOperatingWindow
{
    public DayOfWeek Day { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
}

public sealed class ReservationBookingConditions
{
    public int MinAge { get; set; }
    public int MinLicenseYears { get; set; }
    public int PreparationMinutes { get; set; }
    public string PolicyFingerprint { get; set; } = string.Empty;
    public bool PaymentAtPickup { get; set; }
}
