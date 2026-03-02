namespace RentACar.Core.Entities;

public class PaymentWebhookEvent : BaseEntity
{
    public string ProviderEventId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Processed { get; set; }
}
