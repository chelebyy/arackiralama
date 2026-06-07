namespace RentACar.Core.Enums;

public static class ReservationStatusGroups
{
    public static readonly ReservationStatus[] StockBlocking =
    [
        ReservationStatus.Hold,
        ReservationStatus.UnpaidRequest,
        ReservationStatus.PendingPayment,
        ReservationStatus.Paid,
        ReservationStatus.Confirmed,
        ReservationStatus.Active
    ];

    public static string ToPostgresInFilter(params ReservationStatus[] statuses)
    {
        return string.Join(",", statuses.Select(status => $"'{status}'"));
    }
}
