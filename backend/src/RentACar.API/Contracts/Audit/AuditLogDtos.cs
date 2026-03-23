namespace RentACar.API.Contracts.Audit;

public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    string? UserId,
    DateTime TimestampUtc,
    string? OldValue,
    string? NewValue,
    string? IpAddress,
    string Details);

public sealed record AuditLogListResponse(
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<AuditLogDto> Items);
