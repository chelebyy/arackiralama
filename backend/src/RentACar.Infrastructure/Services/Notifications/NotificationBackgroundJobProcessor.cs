using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentACar.Core.Constants;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public sealed class NotificationBackgroundJobProcessor(
    IApplicationDbContext dbContext,
    INotificationTemplateService notificationTemplateService,
    IEmailProvider emailProvider,
    ISmsProvider smsProvider,
    ILogger<NotificationBackgroundJobProcessor> logger) : INotificationBackgroundJobProcessor
{
    private const int RetryLimit = 3;
    private const int BatchSize = 20;
    private static readonly JsonSerializerOptions JobPayloadSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var jobs = await dbContext.BackgroundJobs
            .Where(x =>
                (x.Type == BackgroundJobTypes.NotificationEmailSend || x.Type == BackgroundJobTypes.NotificationSmsSend) &&
                x.Status == BackgroundJobStatus.Pending &&
                x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var job in jobs)
        {
            try
            {
                job.Status = BackgroundJobStatus.Processing;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                var result = job.Type switch
                {
                    BackgroundJobTypes.NotificationEmailSend => await ProcessEmailJobAsync(job, cancellationToken),
                    BackgroundJobTypes.NotificationSmsSend => await ProcessSmsJobAsync(job, cancellationToken),
                    _ => throw new InvalidOperationException($"Unsupported notification job type: {job.Type}")
                };

                if (!result.Success)
                {
                    throw new InvalidOperationException(result.FailureMessage ?? $"Notification provider returned {result.FailureCode ?? "unknown_error"}.");
                }

                job.Status = BackgroundJobStatus.Completed;
                job.LastError = null;
                job.FailedAt = null;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.UpdatedAt = DateTime.UtcNow;

                if (job.RetryCount >= RetryLimit)
                {
                    job.Status = BackgroundJobStatus.Failed;
                    job.FailedAt = DateTime.UtcNow;
                }
                else
                {
                    job.Status = BackgroundJobStatus.Pending;
                    job.ScheduledAt = DateTime.UtcNow.AddSeconds(15 * job.RetryCount);
                }

                job.LastError = ex.Message;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(ex, "Notification job {BackgroundJobId} failed.", job.Id);
            }
        }

        return processedCount;
    }

    private async Task<NotificationJobResult> ProcessEmailJobAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<QueuedEmailNotificationRequest>(job.Payload, JobPayloadSerializerOptions)
            ?? throw new InvalidOperationException("Email job payload could not be deserialized.");

        var message = notificationTemplateService.RenderEmail(payload);
        var result = await emailProvider.SendAsync(message, cancellationToken);
        return new NotificationJobResult(result.Success, result.FailureCode, result.FailureMessage);
    }

    private async Task<NotificationJobResult> ProcessSmsJobAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<QueuedSmsNotificationRequest>(job.Payload, JobPayloadSerializerOptions)
            ?? throw new InvalidOperationException("SMS job payload could not be deserialized.");

        var message = notificationTemplateService.RenderSms(payload);
        var result = await smsProvider.SendAsync(message, cancellationToken);
        return new NotificationJobResult(result.Success, result.FailureCode, result.FailureMessage);
    }

    private sealed record NotificationJobResult(bool Success, string? FailureCode, string? FailureMessage);
}
