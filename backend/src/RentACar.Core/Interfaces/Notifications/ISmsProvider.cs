namespace RentACar.Core.Interfaces.Notifications;

public interface ISmsProvider
{
    Task<SmsSendResult> SendAsync(
        SmsMessageRequest request,
        CancellationToken cancellationToken = default);
}
