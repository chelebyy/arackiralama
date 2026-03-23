namespace RentACar.API.Services;

using RentACar.API.Contracts.Audit;

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

    Task<AuditLogListResponse> GetPagedAsync(
        string? action,
        string? entityType,
        string? userId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
