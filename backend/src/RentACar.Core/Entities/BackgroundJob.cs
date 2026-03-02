namespace RentACar.Core.Entities;

public class BackgroundJob : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
}
