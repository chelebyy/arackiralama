using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Authentication;
using RentACar.API.Configuration;

namespace RentACar.API.Controllers;

[Authorize(Policy = AuthPolicyNames.AdminOnly)]
[Route("api/admin/v1/security")]
public sealed class AdminSecurityController : BaseApiController
{
    [HttpGet("ping")]
    [EnableRateLimiting(RateLimitPolicyNames.Strict)]
    public IActionResult Ping()
    {
        return OkResponse(new
        {
            status = "authorized",
            role = User.FindFirst(AuthClaimTypes.Role)?.Value ?? "unknown",
            utcTime = DateTime.UtcNow
        });
    }
}
