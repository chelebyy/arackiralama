using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.BackgroundJobs;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/background-jobs")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class AdminBackgroundJobsController(IApplicationDbContext dbContext) : BaseApiController
{
    [HttpGet("failed")]
    public async Task<IActionResult> GetFailed(
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

        var query = dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x => x.Status == BackgroundJobStatus.Failed);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BackgroundJobDto(
                x.Id,
                x.Type,
                x.Status.ToString(),
                x.RetryCount,
                x.LastError,
                x.ScheduledAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return OkResponse(new FailedBackgroundJobsResponse(totalCount, page, pageSize, items));
    }

    [HttpPost("{id:guid}/requeue")]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (job is null)
        {
            return NotFoundResponse("Background job bulunamadı.");
        }

        if (job.Status != BackgroundJobStatus.Failed)
        {
            return BadRequestResponse("Sadece failed durumundaki job tekrar kuyruğa alınabilir.");
        }

        job.Status = BackgroundJobStatus.Pending;
        job.RetryCount = 0;
        job.LastError = null;
        job.FailedAt = null;
        job.ScheduledAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return OkResponse(new { JobId = job.Id }, "Background job tekrar kuyruğa alındı.");
    }
}
