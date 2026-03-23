namespace RentACar.Core.Interfaces.Notifications;

public interface IEmailProvider
{
    Task<EmailSendResult> SendAsync(
        EmailMessageRequest request,
        CancellationToken cancellationToken = default);
}
