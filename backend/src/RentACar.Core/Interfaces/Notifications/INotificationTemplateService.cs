namespace RentACar.Core.Interfaces.Notifications;

public interface INotificationTemplateService
{
    EmailMessageRequest RenderEmail(QueuedEmailNotificationRequest request);

    SmsMessageRequest RenderSms(QueuedSmsNotificationRequest request);
}
