namespace RentACar.Core.Entities;

public sealed class OfficeOperatingPolicy
{
    public GuestManagementPolicy? GuestManagement { get; set; }
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
    public DriverDeclaration? DriverDeclaration { get; set; }
    public int MinAge { get; set; }
    public int MinLicenseYears { get; set; }
    public int PreparationMinutes { get; set; }
    public string PolicyFingerprint { get; set; } = string.Empty;
    public bool PaymentAtPickup { get; set; }
}


public sealed record GuestManagementPolicy
{
    public bool AllowCancellation { get; init; }
    public int? CancellationNoticeMinutes { get; init; }
    public bool AllowDateChange { get; init; }
    public int? ChangeNoticeMinutes { get; init; }
    public decimal? CancellationFee { get; init; }
    public decimal? ChangeFee { get; init; }
}
