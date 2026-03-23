using RentACar.Core.Enums;

namespace RentACar.Core.Entities;

public class BackgroundJob : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Pending;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
}
