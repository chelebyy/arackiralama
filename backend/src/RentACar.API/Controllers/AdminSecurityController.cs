using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RentACar.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/admin/v1/security")]
public sealed class AdminSecurityController : BaseApiController
{
    [HttpGet("ping")]
    [EnableRateLimiting("Strict")]
    public IActionResult Ping()
    {
        return OkResponse(new
        {
            status = "authorized",
            role = User.FindFirst("role")?.Value ?? "unknown",
            utcTime = DateTime.UtcNow
        });
    }
}
