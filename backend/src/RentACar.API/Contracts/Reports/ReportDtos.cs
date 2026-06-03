namespace RentACar.API.Contracts.Reports;

public sealed record RevenueReportBreakdownItemResponse(
    DateOnly Date,
    decimal Revenue,
    int Reservations);

public sealed record RevenueReportResponse(
    string Period,
    decimal TotalRevenue,
    int TotalReservations,
    decimal AverageOrderValue,
    IReadOnlyList<RevenueReportBreakdownItemResponse> DailyBreakdown);

public sealed record OccupancyReportBreakdownItemResponse(
    DateOnly Date,
    int OccupiedVehicles,
    int TotalVehicles,
    decimal OccupancyRate);

public sealed record OccupancyReportResponse(
    string Period,
    int TotalVehicles,
    int OccupiedVehicles,
    decimal OccupancyRate,
    IReadOnlyList<OccupancyReportBreakdownItemResponse> DailyBreakdown);

public sealed record PopularVehicleReportItemResponse(
    string VehicleName,
    int RentalCount,
    decimal Revenue);
