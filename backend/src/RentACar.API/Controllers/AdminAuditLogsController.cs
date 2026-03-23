using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/audit-logs")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminAuditLogsController(IAuditLogService auditLogService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? userId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            return BadRequestResponse("Page değeri 1 veya daha büyük olmalıdır.");
        }

        if (pageSize <= 0 || pageSize > 200)
        {
            return BadRequestResponse("PageSize 1-200 aralığında olmalıdır.");
        }

        var result = await auditLogService.GetPagedAsync(
            action,
            entityType,
            userId,
            fromUtc,
            toUtc,
            page,
            pageSize,
            cancellationToken);

        return OkResponse(result);
    }
}
