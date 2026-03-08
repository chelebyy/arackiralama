using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Configuration;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/pricing")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class PricingController(IPricingService pricingService) : BaseApiController
{
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery(Name = "vehicle_group_id")] Guid vehicleGroupId,
        [FromQuery(Name = "pickup_office_id")] Guid pickupOfficeId,
        [FromQuery(Name = "return_office_id")] Guid returnOfficeId,
        [FromQuery(Name = "pickup_datetime")] DateTime pickupDateTimeUtc,
        [FromQuery(Name = "return_datetime")] DateTime returnDateTimeUtc,
        [FromQuery(Name = "campaign_code")] string? campaignCode,
        [FromQuery(Name = "extra_driver_count")] int extraDriverCount,
        [FromQuery(Name = "child_seat_count")] int childSeatCount,
        [FromQuery(Name = "driver_age")] int? driverAge,
        [FromQuery(Name = "full_coverage_waiver")] bool fullCoverageWaiver,
        CancellationToken cancellationToken)
    {
        if (vehicleGroupId == Guid.Empty)
        {
            return BadRequestResponse("Gecerli bir arac grubu secilmelidir.");
        }

        if (pickupOfficeId == Guid.Empty || returnOfficeId == Guid.Empty)
        {
            return BadRequestResponse("Gecerli pickup ve return ofisleri zorunludur.");
        }

        if (pickupDateTimeUtc >= returnDateTimeUtc)
        {
            return BadRequestResponse("Alis tarihi donus tarihinden once olmalidir.");
        }

        if (extraDriverCount < 0 || childSeatCount < 0)
        {
            return BadRequestResponse("Ek surucu ve cocuk koltugu adetleri negatif olamaz.");
        }

        if (driverAge.HasValue && driverAge.Value < 18)
        {
            return BadRequestResponse("Surucu yasi 18 veya daha buyuk olmalidir.");
        }

        if (!await pricingService.VehicleGroupExistsAsync(vehicleGroupId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir arac grubu secilmelidir.");
        }

        if (!await pricingService.OfficeExistsAsync(pickupOfficeId, cancellationToken) ||
            !await pricingService.OfficeExistsAsync(returnOfficeId, cancellationToken))
        {
            return BadRequestResponse("Gecerli pickup ve return ofisleri zorunludur.");
        }

        var rentalDays = CalculateRentalDays(pickupDateTimeUtc, returnDateTimeUtc);
        if (!string.IsNullOrWhiteSpace(campaignCode))
        {
            var isCampaignCodeValid = await pricingService.IsCampaignCodeValidAsync(
                campaignCode,
                vehicleGroupId,
                rentalDays,
                DateOnly.FromDateTime(pickupDateTimeUtc),
                cancellationToken);

            if (!isCampaignCodeValid)
            {
                return BadRequestResponse("Gecersiz veya suresi dolmus kampanya kodu.");
            }
        }

        var breakdown = await pricingService.CalculateBreakdownAsync(
            vehicleGroupId,
            pickupOfficeId,
            returnOfficeId,
            pickupDateTimeUtc,
            returnDateTimeUtc,
            campaignCode,
            extraDriverCount,
            childSeatCount,
            driverAge,
            fullCoverageWaiver,
            cancellationToken);

        if (breakdown is null)
        {
            return BadRequestResponse("Secilen tarih araliginda fiyatlandirma kurali bulunamadi.");
        }

        return OkResponse(breakdown);
    }

    private static int CalculateRentalDays(DateTime pickupDateTimeUtc, DateTime returnDateTimeUtc)
    {
        var pickupDate = DateOnly.FromDateTime(pickupDateTimeUtc);
        var returnDate = DateOnly.FromDateTime(returnDateTimeUtc);
        return Math.Max(1, returnDate.DayNumber - pickupDate.DayNumber);
    }
}
