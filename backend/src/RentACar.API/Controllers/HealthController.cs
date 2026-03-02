using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RentACar.API.Controllers;

[Route("api/v1/[controller]")]
public class HealthController : BaseApiController
{
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("Health")]
    public IActionResult Get() =>
        OkResponse(new
        {
            status = "healthy",
            utcTime = DateTime.UtcNow
        });
}
