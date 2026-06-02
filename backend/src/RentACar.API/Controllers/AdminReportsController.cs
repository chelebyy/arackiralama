using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/reports")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminReportsController(IReportsService reportsService) : BaseApiController
{
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] string period,
        CancellationToken cancellationToken)
    {
        var result = await reportsService.GetRevenueReportAsync(period, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("occupancy")]
    public async Task<IActionResult> GetOccupancy(
        [FromQuery] string period,
        CancellationToken cancellationToken)
    {
        var result = await reportsService.GetOccupancyReportAsync(period, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("popular-vehicles")]
    public async Task<IActionResult> GetPopularVehicles(
        [FromQuery] string period,
        CancellationToken cancellationToken)
    {
        var result = await reportsService.GetPopularVehiclesAsync(period, cancellationToken);
        return OkResponse(result);
    }
}
