namespace RentACar.Core.Interfaces.Notifications;

public interface INotificationBackgroundJobProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
