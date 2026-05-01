using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/offices")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class OfficesController(IFleetService fleetService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var offices = await fleetService.GetOfficesAsync(cancellationToken);
        return OkResponse(offices);
    }
}
