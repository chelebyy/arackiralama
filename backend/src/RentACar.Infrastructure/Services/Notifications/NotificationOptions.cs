namespace RentACar.Infrastructure.Services.Notifications;

/// <summary>
/// Top-level notification configuration bound to the <c>Notifications</c> config section.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public string DefaultLocale { get; init; } = "tr-TR";
    public SmsNotificationOptions Sms { get; init; } = new();
    public EmailNotificationOptions Email { get; init; } = new();
}

/// <summary>
/// SMS provider selection and per-provider credentials. </summary>
public sealed class SmsNotificationOptions
{
    public string PrimaryProvider { get; init; } = "Netgsm";
    public bool EnableFallback { get; init; } = true;
    public NetgsmSmsOptions Netgsm { get; init; } = new();
    public TwilioSmsOptions Twilio { get; init; } = new();
}

/// <summary>
/// Netgsm API credentials and endpoint. </summary>
public sealed class NetgsmSmsOptions
{
    public string BaseUrl { get; init; } = "https://api.netgsm.com.tr";
    public string Usercode { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string MsgHeader { get; init; } = string.Empty;
    public string DefaultEncoding { get; init; } = "TR";
}

/// <summary>
/// Twilio API credentials. </summary>
public sealed class TwilioSmsOptions
{
    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;
    public string FromPhoneNumber { get; init; } = string.Empty;
    public string? StatusCallbackUrl { get; init; }
}

/// <summary>
/// Email dispatch settings and SMTP credentials. </summary>
public sealed class EmailNotificationOptions
{
    public bool Enabled { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public SmtpOptions Smtp { get; init; } = new();
}

/// <summary>
/// SMTP connection parameters. </summary>
public sealed class SmtpOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool EnableSsl { get; init; } = true;
}
