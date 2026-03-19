namespace RentACar.API.Services;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityType,
        string entityId,
        string? userId,
        string? oldValue,
        string? newValue,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
