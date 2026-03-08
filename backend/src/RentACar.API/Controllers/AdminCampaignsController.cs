using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/campaigns")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminCampaignsController(IPricingService pricingService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var campaigns = await pricingService.GetCampaignsAsync(cancellationToken);
        return OkResponse(campaigns);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(request, null, cancellationToken);
        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var createdCampaign = await pricingService.CreateCampaignAsync(request, cancellationToken);
        return OkResponse(createdCampaign, "Kampanya olusturuldu.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCampaignRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(
            new CreateCampaignRequest(
                request.Code,
                request.DiscountType,
                request.DiscountValue,
                request.MinDays,
                request.ValidFrom,
                request.ValidUntil,
                request.IsActive,
                request.AllowedVehicleGroupIds),
            id,
            cancellationToken);

        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var updatedCampaign = await pricingService.UpdateCampaignAsync(id, request, cancellationToken);
        if (updatedCampaign is null)
        {
            return NotFound(ApiResponse<object>.Fail("Kampanya bulunamadi."));
        }

        return OkResponse(updatedCampaign, "Kampanya guncellendi.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await pricingService.DeleteCampaignAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.Fail("Kampanya bulunamadi."));
        }

        return OkResponse(new { Id = id }, "Kampanya silindi.");
    }

    private async Task<string?> ValidateAsync(CreateCampaignRequest request, Guid? existingCampaignId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Kampanya kodu zorunludur.";
        }

        if (!PricingService.IsSupportedCampaignDiscountType(request.DiscountType))
        {
            return "Desteklenmeyen indirim tipi. Degerler: percentage, fixed.";
        }

        if (request.DiscountValue <= 0m)
        {
            return "Indirim tutari sifirdan buyuk olmalidir.";
        }

        if (request.DiscountType.Trim().Equals("percentage", StringComparison.OrdinalIgnoreCase) && request.DiscountValue > 100m)
        {
            return "Yuzdesel indirim 100'den buyuk olamaz.";
        }

        if (request.MinDays < 1)
        {
            return "Minimum kiralama gunu 1 veya daha buyuk olmalidir.";
        }

        if (request.ValidFrom > request.ValidUntil)
        {
            return "Kampanya baslangic tarihi bitis tarihinden sonra olamaz.";
        }

        if (!await pricingService.IsCampaignCodeAvailableAsync(request.Code, existingCampaignId, cancellationToken))
        {
            return "Bu kampanya kodu zaten kullaniliyor.";
        }

        var vehicleGroupIds = (request.AllowedVehicleGroupIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (!await pricingService.VehicleGroupsExistAsync(vehicleGroupIds, cancellationToken))
        {
            return "Kampanya icin gecersiz arac grubu secildi.";
        }

        return null;
    }
}
