using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.FeatureFlags;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/feature-flags")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminFeatureFlagsController(IFeatureFlagService featureFlagService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var flags = await featureFlagService.GetAllAsync(cancellationToken);
        return OkResponse(flags);
    }

    [HttpPatch("{name}")]
    public async Task<IActionResult> Update(
        string name,
        [FromBody] UpdateFeatureFlagRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequestResponse("Feature flag adı zorunludur.");
        }

        var updated = await featureFlagService.UpdateAsync(name, request.Enabled, cancellationToken);
        if (updated is null)
        {
            return NotFound(ApiResponse<object>.Fail("Feature flag bulunamadı."));
        }

        return OkResponse(updated, "Feature flag güncellendi.");
    }
}
