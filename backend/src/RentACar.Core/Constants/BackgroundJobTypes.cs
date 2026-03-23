namespace RentACar.Core.Constants;

public static class BackgroundJobTypes
{
    public const string NotificationEmailSend = "notification-email-send";
    public const string NotificationSmsSend = "notification-sms-send";
    public const string PaymentWebhookProcess = "payment-webhook-process";
    public const string ReservationHoldReleaseExpired = "reservation-hold-release-expired";
    public const string DailyBackupRun = "daily-backup-run";
}
