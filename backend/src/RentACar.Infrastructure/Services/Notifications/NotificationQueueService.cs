using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentACar.Core.Constants;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public sealed class NotificationQueueService(
    IApplicationDbContext dbContext,
    ILogger<NotificationQueueService> logger) : INotificationQueueService
{
    public const string SendEmailJobType = BackgroundJobTypes.NotificationEmailSend;
    public const string SendSmsJobType = BackgroundJobTypes.NotificationSmsSend;

    public Task<Guid> EnqueueEmailAsync(
        QueuedEmailNotificationRequest request,
        DateTime? scheduledAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(SendEmailJobType, request, scheduledAtUtc, cancellationToken);
    }

    public Task<Guid> EnqueueSmsAsync(
        QueuedSmsNotificationRequest request,
        DateTime? scheduledAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        return EnqueueSmsInternalAsync(request, scheduledAtUtc, cancellationToken);
    }

    private async Task<Guid> EnqueueAsync<TPayload>(
        string jobType,
        TPayload payload,
        DateTime? scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        var job = new BackgroundJob
        {
            Type = jobType,
            Payload = JsonSerializer.Serialize(payload),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = scheduledAtUtc ?? DateTime.UtcNow
        };

        await dbContext.BackgroundJobs.AddAsync(job, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }

    private async Task<Guid> EnqueueSmsInternalAsync(
        QueuedSmsNotificationRequest request,
        DateTime? scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        FeatureFlag? smsFlag;

        try
        {
            smsFlag = await dbContext.FeatureFlags
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name == NotificationConstants.EnableSmsNotificationsFlag, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to query SMS feature flag. Continuing with SMS enqueue enabled.");
            smsFlag = null;
        }

        if (smsFlag is { Enabled: false })
        {
            return Guid.Empty;
        }

        return await EnqueueAsync(SendSmsJobType, request, scheduledAtUtc, cancellationToken);
    }
}
