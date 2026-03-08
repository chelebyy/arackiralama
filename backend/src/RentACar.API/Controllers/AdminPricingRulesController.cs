using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Pricing;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/pricing-rules")]
[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminPricingRulesController(IPricingService pricingService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var pricingRules = await pricingService.GetPricingRulesAsync(cancellationToken);
        return OkResponse(pricingRules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(
            request.VehicleGroupId,
            request.StartDate,
            request.EndDate,
            request.DailyPrice,
            request.Multiplier,
            request.WeekdayMultiplier,
            request.WeekendMultiplier,
            request.CalculationType,
            request.Priority,
            null,
            cancellationToken);

        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var createdPricingRule = await pricingService.CreatePricingRuleAsync(request, cancellationToken);
        return OkResponse(createdPricingRule, "Pricing rule olusturuldu.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(
            request.VehicleGroupId,
            request.StartDate,
            request.EndDate,
            request.DailyPrice,
            request.Multiplier,
            request.WeekdayMultiplier,
            request.WeekendMultiplier,
            request.CalculationType,
            request.Priority,
            id,
            cancellationToken);

        if (validationError is not null)
        {
            return BadRequestResponse(validationError);
        }

        var updatedPricingRule = await pricingService.UpdatePricingRuleAsync(id, request, cancellationToken);
        if (updatedPricingRule is null)
        {
            return NotFound(ApiResponse<object>.Fail("Pricing rule bulunamadi."));
        }

        return OkResponse(updatedPricingRule, "Pricing rule guncellendi.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await pricingService.DeletePricingRuleAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.Fail("Pricing rule bulunamadi."));
        }

        return OkResponse(new { Id = id }, "Pricing rule silindi.");
    }

    private async Task<string?> ValidateAsync(
        Guid vehicleGroupId,
        DateOnly startDate,
        DateOnly endDate,
        decimal dailyPrice,
        decimal multiplier,
        decimal weekdayMultiplier,
        decimal weekendMultiplier,
        string calculationType,
        int priority,
        Guid? existingPricingRuleId,
        CancellationToken cancellationToken)
    {
        if (vehicleGroupId == Guid.Empty)
        {
            return "Gecerli bir arac grubu secilmelidir.";
        }

        if (startDate > endDate)
        {
            return "Baslangic tarihi bitis tarihinden sonra olamaz.";
        }

        if (dailyPrice <= 0m)
        {
            return "Gunluk fiyat sifirdan buyuk olmalidir.";
        }

        if (priority < 0)
        {
            return "Oncelik negatif olamaz.";
        }

        if (!PricingService.IsSupportedPricingCalculationType(calculationType))
        {
            return "Desteklenmeyen fiyat hesaplama tipi. Degerler: multiplier, fixed.";
        }

        if (PricingService.IsSupportedPricingCalculationType(calculationType) &&
            calculationType.Trim().Equals("multiplier", StringComparison.OrdinalIgnoreCase) &&
            multiplier <= 0m)
        {
            return "Multiplier tipi icin carpan sifirdan buyuk olmalidir.";
        }

        if (weekdayMultiplier <= 0m || weekendMultiplier <= 0m)
        {
            return "Hafta ici ve hafta sonu carpani sifirdan buyuk olmalidir.";
        }

        if (!await pricingService.VehicleGroupExistsAsync(vehicleGroupId, cancellationToken))
        {
            return "Gecerli bir arac grubu secilmelidir.";
        }

        if (await pricingService.HasConflictingPricingRuleAsync(vehicleGroupId, startDate, endDate, priority, existingPricingRuleId, cancellationToken))
        {
            return "Ayni arac grubu ve oncelik icin cakisan tarih araliginda baska bir pricing rule bulunuyor.";
        }

        return null;
    }
}
