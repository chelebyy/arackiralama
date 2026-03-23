using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Audit;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class AuditLogService(IApplicationDbContext dbContext) : IAuditLogService
{
    public async Task LogAsync(
        string action,
        string entityType,
        string entityId,
        string? userId,
        string? oldValue,
        string? newValue,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow,
            Details = BuildDetails(action, entityType, entityId)
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditLogListResponse> GetPagedAsync(
        string? action,
        string? entityType,
        string? userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action.Contains(action.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(x => x.UserId == userId.Trim());
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.Timestamp >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.Timestamp <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogDto(
                x.Id,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.UserId,
                x.Timestamp,
                x.OldValue,
                x.NewValue,
                x.IpAddress,
                x.Details))
            .ToListAsync(cancellationToken);

        return new AuditLogListResponse(totalCount, page, pageSize, items);
    }

    private static string BuildDetails(string action, string entityType, string entityId) =>
        $"{action} islemi {entityType} (ID: {entityId}) uzerinde gerceklestirildi.";
}
