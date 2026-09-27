using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/reservation-extra-options")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReservationExtraOptionsController(
    IReservationExtraOptionCatalogService catalogService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid vehicleGroupId,
        [FromQuery] string locale,
        CancellationToken cancellationToken,
        [FromQuery] Guid? vehicleId = null)
    {
        try
        {
            return OkResponse(vehicleId.HasValue
                ? await catalogService.GetPublicVehicleCatalogAsync(vehicleId.Value, locale, cancellationToken)
                : await catalogService.GetPublicCatalogAsync(vehicleGroupId, locale, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequestResponse(exception.Message);
        }
    }
}
