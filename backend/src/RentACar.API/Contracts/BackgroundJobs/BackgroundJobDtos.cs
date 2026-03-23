namespace RentACar.API.Contracts.BackgroundJobs;

public sealed record BackgroundJobDto(
    Guid Id,
    string Type,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime ScheduledAtUtc,
    DateTime UpdatedAtUtc);

public sealed record FailedBackgroundJobsResponse(
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<BackgroundJobDto> Items);
