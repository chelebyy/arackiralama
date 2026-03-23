namespace RentACar.Core.Interfaces.Notifications;

public interface INotificationQueueService
{
    Task<Guid> EnqueueEmailAsync(
        QueuedEmailNotificationRequest request,
        DateTime? scheduledAtUtc = null,
        CancellationToken cancellationToken = default);

    Task<Guid> EnqueueSmsAsync(
        QueuedSmsNotificationRequest request,
        DateTime? scheduledAtUtc = null,
        CancellationToken cancellationToken = default);
}
