using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/public-site-settings")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminPublicSiteSettingsController(IPublicSiteSettingsService settingsService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return OkResponse(settings);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdatePublicSiteSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsService.UpdateAsync(request, cancellationToken);
            return OkResponse(settings, "Public site ayarları güncellendi.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
