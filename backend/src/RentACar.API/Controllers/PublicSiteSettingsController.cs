using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/v1/public-site-settings")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class PublicSiteSettingsController(IPublicSiteSettingsService settingsService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return OkResponse(settings);
    }
}
