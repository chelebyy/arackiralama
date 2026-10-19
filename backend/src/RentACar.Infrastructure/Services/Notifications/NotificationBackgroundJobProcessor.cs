using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    ILogger<NotificationBackgroundJobProcessor> logger,
    IOptions<BackgroundJobProcessorOptions>? backgroundJobProcessorOptions = null) : INotificationBackgroundJobProcessor
{
    private const int RetryLimit = 3;
    private const string ObsoleteReminderReason = "Reservation reminder is no longer current.";
    private readonly BackgroundJobProcessorOptions _backgroundJobProcessorOptions = backgroundJobProcessorOptions?.Value ?? new();
    private static readonly JsonSerializerOptions JobPayloadSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var abandonedBefore = now.AddMinutes(-5);
        var jobs = await dbContext.BackgroundJobs
            .Where(x =>
                (x.Type == BackgroundJobTypes.NotificationEmailSend || x.Type == BackgroundJobTypes.NotificationSmsSend) &&
                (x.Status == BackgroundJobStatus.Pending ||
                 (x.Status == BackgroundJobStatus.Processing && x.UpdatedAt < abandonedBefore)) &&
                x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .Take(Math.Max(_backgroundJobProcessorOptions.BatchSize, 1))
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var job in jobs)
        {
            try
            {
                if (dbContext is DbContext context && context.Database.IsRelational())
                {
                    var claimed = await dbContext.BackgroundJobs.Where(x => x.Id == job.Id &&
                        (x.Status == BackgroundJobStatus.Pending ||
                         (x.Status == BackgroundJobStatus.Processing && x.UpdatedAt < abandonedBefore)) &&
                        x.ScheduledAt <= now).ExecuteUpdateAsync(update => update
                            .SetProperty(x => x.Status, BackgroundJobStatus.Processing)
                            .SetProperty(x => x.UpdatedAt, now), cancellationToken);
                    if (claimed == 0) continue;
                }
                job.Status = BackgroundJobStatus.Processing;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                var result = job.Type switch
                {
                    BackgroundJobTypes.NotificationEmailSend => await ProcessEmailJobAsync(job, cancellationToken),
                    BackgroundJobTypes.NotificationSmsSend => await ProcessSmsJobAsync(job, cancellationToken),
                    _ => throw new InvalidOperationException($"Unsupported notification job type: {job.Type}")
                };

                if (result.Skipped)
                {
                    job.Status = BackgroundJobStatus.Cancelled;
                    job.LastError = result.FailureMessage;
                    job.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

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
                var guestMail = job.Payload.Contains("guest-reservation-", StringComparison.Ordinal);
                job.LastError = guestMail ? "Guest notification delivery failed." : ex.Message;
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

                await dbContext.SaveChangesAsync(cancellationToken);
                if (guestMail) logger.LogError("Guest notification delivery failed; retry {RetryCount}.", job.RetryCount);
                else logger.LogError(ex, "Notification job {BackgroundJobId} failed.", job.Id);
            }
        }

        return processedCount;
    }

    private async Task<NotificationJobResult> ProcessEmailJobAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<QueuedEmailNotificationRequest>(job.Payload, JobPayloadSerializerOptions)
            ?? throw new InvalidOperationException("Email job payload could not be deserialized.");

        if (await IsUnusableAccessAsync(payload, cancellationToken))
            return new NotificationJobResult(true, null, "Guest access code is no longer usable.", Skipped: true);
        if (await IsObsoleteReminderAsync(job, payload.TemplateKey, payload.Variables, cancellationToken))
            return new NotificationJobResult(true, null, ObsoleteReminderReason, Skipped: true);
        var message = notificationTemplateService.RenderEmail(payload);
        if (await IsUnusableAccessAsync(payload, cancellationToken))
            return new NotificationJobResult(true, null, "Guest access code is no longer usable.", Skipped: true);
        if (await IsObsoleteReminderAsync(job, payload.TemplateKey, payload.Variables, cancellationToken))
            return new NotificationJobResult(true, null, ObsoleteReminderReason, Skipped: true);
        var result = await emailProvider.SendAsync(message, cancellationToken);
        return new NotificationJobResult(result.Success, result.FailureCode, result.FailureMessage);
    }

    private async Task<bool> IsUnusableAccessAsync(QueuedEmailNotificationRequest payload, CancellationToken ct)
    {
        if (GuestReservationMail.IsExpiredAccess(payload, DateTimeOffset.UtcNow)) return true;
        if (payload.TemplateKey != "guest-reservation-access" ||
            !payload.Variables.TryGetValue("ChallengeId", out var challenge)) return false;
        if (!Guid.TryParse(challenge, out var id)) return true;
        return !await dbContext.GuestReservationAccess.AsNoTracking().AnyAsync(g => g.Id == id &&
            !g.Revoked && g.VerifiedAt == null && g.Attempts < 5 && g.CodeExpiresAt > DateTime.UtcNow, ct);
    }

    private async Task<NotificationJobResult> ProcessSmsJobAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<QueuedSmsNotificationRequest>(job.Payload, JobPayloadSerializerOptions)
            ?? throw new InvalidOperationException("SMS job payload could not be deserialized.");

        if (await IsObsoleteReminderAsync(job, payload.TemplateKey, payload.Variables, cancellationToken))
            return new NotificationJobResult(true, null, ObsoleteReminderReason, Skipped: true);
        var message = notificationTemplateService.RenderSms(payload);
        if (await IsObsoleteReminderAsync(job, payload.TemplateKey, payload.Variables, cancellationToken))
            return new NotificationJobResult(true, null, ObsoleteReminderReason, Skipped: true);
        var result = await smsProvider.SendAsync(message, cancellationToken);
        return new NotificationJobResult(result.Success, result.FailureCode, result.FailureMessage);
    }

    private async Task<bool> IsObsoleteReminderAsync(BackgroundJob job, string templateKey,
        IReadOnlyDictionary<string, string> variables, CancellationToken ct)
    {
        if (templateKey is not (NotificationTemplateKeys.PickupReminder or NotificationTemplateKeys.ReturnReminder))
            return false;
        if (!variables.TryGetValue("PublicCode", out var publicCode) || string.IsNullOrWhiteSpace(publicCode))
            return true;
        return !await dbContext.Reservations.AsNoTracking().AnyAsync(r => r.PublicCode == publicCode &&
            r.Status != ReservationStatus.Cancelled && !dbContext.ReservationAmendments.Any(a =>
                a.ReservationId == r.Id && a.AppliedAt > job.CreatedAt), ct);
    }

    private sealed record NotificationJobResult(bool Success, string? FailureCode, string? FailureMessage, bool Skipped = false);
}
