using RentACar.API.Contracts.Reports;

namespace RentACar.API.Services;

public interface IReportsService
{
    Task<RevenueReportResponse> GetRevenueReportAsync(string period, CancellationToken cancellationToken = default);

    Task<OccupancyReportResponse> GetOccupancyReportAsync(string period, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PopularVehicleReportItemResponse>> GetPopularVehiclesAsync(string period, CancellationToken cancellationToken = default);
}
