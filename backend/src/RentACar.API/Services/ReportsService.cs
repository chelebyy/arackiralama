using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Reports;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class ReportsService(IApplicationDbContext dbContext) : IReportsService
{
    private const int PopularVehiclesTopN = 5;

    private static readonly HashSet<ReservationStatus> RevenueEligibleStatuses = new()
    {
        ReservationStatus.Paid,
        ReservationStatus.Active,
        ReservationStatus.Completed
    };

    public async Task<RevenueReportResponse> GetRevenueReportAsync(
        string period,
        CancellationToken cancellationToken = default)
    {
        var range = ResolvePeriod(period);
        if (range is null)
        {
            return EmptyRevenueReport(period);
        }

        var (startUtc, endUtc, days) = range.Value;

        var reservations = await dbContext.Reservations
            .AsNoTracking()
            .Where(r => RevenueEligibleStatuses.Contains(r.Status)
                        && r.PickupDateTime >= startUtc
                        && r.PickupDateTime < endUtc)
            .Select(r => new { r.Id, r.PickupDateTime })
            .ToListAsync(cancellationToken);

        var reservationIds = reservations.Select(r => r.Id).ToList();
        var reservationPickupLookup = reservations.ToDictionary(r => r.Id, r => r.PickupDateTime);

        var paymentIntents = await dbContext.PaymentIntents
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Succeeded
                        && reservationIds.Contains(p.ReservationId))
            .Select(p => new { p.Amount, p.ReservationId })
            .ToListAsync(cancellationToken);

        var totalRevenue = paymentIntents.Sum(p => p.Amount);
        var totalReservations = reservations.Count;
        var averageOrderValue = totalReservations > 0
            ? Math.Round(totalRevenue / totalReservations, 2)
            : 0m;

        var breakdown = days
            .Select(day =>
            {
                var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);

                var dayRevenue = paymentIntents
                    .Where(p => reservationPickupLookup[p.ReservationId] >= dayStart
                                && reservationPickupLookup[p.ReservationId] < dayEnd)
                    .Sum(p => p.Amount);

                var dayReservations = reservations
                    .Count(r => r.PickupDateTime >= dayStart && r.PickupDateTime < dayEnd);

                return new RevenueReportBreakdownItemResponse(day, dayRevenue, dayReservations);
            })
            .ToList();

        return new RevenueReportResponse(
            period,
            totalRevenue,
            totalReservations,
            averageOrderValue,
            breakdown);
    }

    public async Task<OccupancyReportResponse> GetOccupancyReportAsync(
        string period,
        CancellationToken cancellationToken = default)
    {
        var range = ResolvePeriod(period);
        if (range is null)
        {
            return EmptyOccupancyReport(period);
        }

        var (startUtc, endUtc, days) = range.Value;

        var totalVehicles = await dbContext.Vehicles
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var reservations = await dbContext.Reservations
            .AsNoTracking()
            .Where(r => RevenueEligibleStatuses.Contains(r.Status)
                        && r.PickupDateTime < endUtc
                        && r.ReturnDateTime > startUtc)
            .Select(r => new { r.PickupDateTime, r.ReturnDateTime })
            .ToListAsync(cancellationToken);

        var breakdown = days
            .Select(day =>
            {
                var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);

                var occupied = reservations.Count(r =>
                    r.PickupDateTime < dayEnd && r.ReturnDateTime > dayStart);

                var rate = totalVehicles > 0
                    ? Math.Round((decimal)occupied / totalVehicles * 100m, 2)
                    : 0m;

                return new OccupancyReportBreakdownItemResponse(day, occupied, totalVehicles, rate);
            })
            .ToList();

        var lastBucket = breakdown[^1];
        var overallRate = lastBucket.OccupancyRate;
        var totalOccupied = lastBucket.OccupiedVehicles;

        return new OccupancyReportResponse(
            period,
            totalVehicles,
            totalOccupied,
            overallRate,
            breakdown);
    }

    public async Task<IReadOnlyList<PopularVehicleReportItemResponse>> GetPopularVehiclesAsync(
        string period,
        CancellationToken cancellationToken = default)
    {
        var range = ResolvePeriod(period);
        if (range is null)
        {
            return Array.Empty<PopularVehicleReportItemResponse>();
        }

        var (startUtc, endUtc, _) = range.Value;

        var scopedReservations = await dbContext.Reservations
            .AsNoTracking()
            .Where(r => RevenueEligibleStatuses.Contains(r.Status)
                        && r.PickupDateTime >= startUtc
                        && r.PickupDateTime < endUtc)
            .Select(r => new { r.Id, r.VehicleId })
            .ToListAsync(cancellationToken);

        var grouped = scopedReservations
            .GroupBy(r => r.VehicleId)
            .Select(g => new
            {
                VehicleId = g.Key,
                RentalCount = g.Count()
            })
            .ToList();

        if (grouped.Count == 0)
        {
            return Array.Empty<PopularVehicleReportItemResponse>();
        }

        var vehicleIds = grouped.Select(g => g.VehicleId).ToList();

        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Brand, v.Model })
            .ToListAsync(cancellationToken);

        var reservationVehicleLookup = scopedReservations.ToDictionary(r => r.Id, r => r.VehicleId);
        var reservationIds = reservationVehicleLookup.Keys.ToList();

        var paymentAmounts = await dbContext.PaymentIntents
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Succeeded
                        && reservationIds.Contains(p.ReservationId))
            .Select(p => new { p.ReservationId, p.Amount })
            .ToListAsync(cancellationToken);

        var revenueLookup = paymentAmounts
            .GroupBy(p => reservationVehicleLookup[p.ReservationId])
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var result = grouped
            .OrderByDescending(g => g.RentalCount)
            .ThenBy(g => g.VehicleId)
            .Take(PopularVehiclesTopN)
            .Select(g =>
            {
                var vehicle = vehicles.FirstOrDefault(v => v.Id == g.VehicleId);
                var name = vehicle is null
                    ? "Unknown"
                    : $"{vehicle.Brand} {vehicle.Model}".Trim();
                var revenue = revenueLookup.TryGetValue(g.VehicleId, out var r) ? r : 0m;
                return new PopularVehicleReportItemResponse(name, g.RentalCount, revenue);
            })
            .ToList();

        return result;
    }

    private static (DateTime StartUtc, DateTime EndUtc, IReadOnlyList<DateOnly> Days)? ResolvePeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return null;
        }

        var normalized = period.Trim().ToLowerInvariant();
        var dayCount = normalized switch
        {
            "daily" => 1,
            "weekly" => 7,
            "monthly" => 30,
            "quarterly" => 90,
            "yearly" => 365,
            _ => -1
        };

        if (dayCount < 0)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-(dayCount - 1));
        var endUtc = today.AddDays(1);
        var startUtc = startDate;

        var days = Enumerable.Range(0, dayCount)
            .Select(i => DateOnly.FromDateTime(startDate.AddDays(i)))
            .ToList();

        return (startUtc, endUtc, days);
    }

    private static RevenueReportResponse EmptyRevenueReport(string? period) =>
        new(period ?? string.Empty, 0m, 0, 0m, Array.Empty<RevenueReportBreakdownItemResponse>());

    private static OccupancyReportResponse EmptyOccupancyReport(string? period) =>
        new(period ?? string.Empty, 0, 0, 0m, Array.Empty<OccupancyReportBreakdownItemResponse>());
}
