using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/vehicles")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class VehiclesController(IFleetService fleetService) : BaseApiController
{
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery(Name = "office_id")] Guid officeId,
        [FromQuery(Name = "pickup_datetime")] DateTime pickupDateTimeUtc,
        [FromQuery(Name = "return_datetime")] DateTime returnDateTimeUtc,
        [FromQuery(Name = "vehicle_group_id")] Guid? vehicleGroupId,
        CancellationToken cancellationToken)
    {
        if (officeId == Guid.Empty)
        {
            return BadRequestResponse("Gecerli bir ofis secilmelidir.");
        }

        if (pickupDateTimeUtc >= returnDateTimeUtc)
        {
            return BadRequestResponse("Alis tarihi donus tarihinden once olmalidir.");
        }

        if (!await fleetService.OfficeExistsAsync(officeId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir ofis secilmelidir.");
        }

        if (vehicleGroupId.HasValue && !await fleetService.VehicleGroupExistsAsync(vehicleGroupId.Value, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir arac grubu secilmelidir.");
        }

        var availableGroups = await fleetService.SearchAvailableVehicleGroupsAsync(
            officeId,
            pickupDateTimeUtc,
            returnDateTimeUtc,
            vehicleGroupId,
            cancellationToken);

        return OkResponse(availableGroups);
    }
}
