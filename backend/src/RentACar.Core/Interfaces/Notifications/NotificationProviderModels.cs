namespace RentACar.Core.Interfaces.Notifications;

public static class NotificationTemplateKeys
{
    public const string PasswordResetCustomer = "password-reset-customer";
    public const string PasswordResetAdmin = "password-reset-admin";
    public const string ReservationConfirmed = "reservation-confirmed";
    public const string ReservationCancelled = "reservation-cancelled";
    public const string PaymentReceived = "payment-received";
    public const string DepositReleased = "deposit-released";
    public const string PickupReminder = "pickup-reminder";
    public const string ReturnReminder = "return-reminder";
}

public record EmailMessageRequest
{
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string PlainTextBody { get; init; } = string.Empty;
    public string? HtmlBody { get; init; }
}

public record EmailSendResult
{
    public bool Success { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? ProviderMessageId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
}

public record SmsMessageRequest
{
    public string ToPhoneNumber { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public record QueuedEmailNotificationRequest
{
    public string ToEmail { get; init; } = string.Empty;
    public string TemplateKey { get; init; } = string.Empty;
    public string Locale { get; init; } = "tr-TR";
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();
}

public record QueuedSmsNotificationRequest
{
    public string ToPhoneNumber { get; init; } = string.Empty;
    public string TemplateKey { get; init; } = string.Empty;
    public string Locale { get; init; } = "tr-TR";
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();
}

public record SmsSendResult
{
    public bool Success { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? ProviderMessageId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
}
