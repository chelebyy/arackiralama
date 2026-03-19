namespace RentACar.Core.Entities;

public class AuditLog : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string Details { get; set; } = string.Empty;
}
