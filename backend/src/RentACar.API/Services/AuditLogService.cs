using System.Text.Json;
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

    private static string BuildDetails(string action, string entityType, string entityId) =>
        $"{action} islemi {entityType} (ID: {entityId}) uzerinde gerceklestirildi.";
}
