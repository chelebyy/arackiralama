using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RentACar.Core.Constants;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.Worker;

public sealed class Worker(
    IServiceProvider serviceProvider,
    IOptions<DailyBackupOptions> dailyBackupOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int HoldReleaseRetryLimit = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationJobsAsync(stoppingToken);
                await EnqueueExpiredHoldJobsAsync(stoppingToken);
                await ProcessExpiredHoldJobsAsync(stoppingToken);
                await EnsureDailyBackupJobScheduledAsync(stoppingToken);
                await ProcessDailyBackupJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process background jobs");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessNotificationJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<INotificationBackgroundJobProcessor>();
        var processedCount = await processor.ProcessPendingAsync(cancellationToken);

        if (processedCount > 0)
        {
            logger.LogInformation("Processed {ProcessedCount} notification background jobs", processedCount);
        }
    }

    private async Task EnqueueExpiredHoldJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var expiredReservationIds = await holdService.GetExpiredHoldsAsync(cancellationToken);
        if (expiredReservationIds.Count == 0)
        {
            return;
        }

        var enqueuedCount = 0;
        var now = DateTime.UtcNow;
        var runnablePayloads = await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x =>
                x.Type == BackgroundJobTypes.ReservationHoldReleaseExpired
                && (x.Status == BackgroundJobStatus.Pending || x.Status == BackgroundJobStatus.Processing))
            .Select(x => x.Payload)
            .ToListAsync(cancellationToken);

        foreach (var reservationId in expiredReservationIds)
        {
            var hasRunnableJob = runnablePayloads.Any(payload =>
                WorkerPayloadMatcher.HasReservationId(payload, reservationId));

            if (hasRunnableJob)
            {
                continue;
            }

            dbContext.BackgroundJobs.Add(new BackgroundJob
            {
                Type = BackgroundJobTypes.ReservationHoldReleaseExpired,
                Payload = JsonSerializer.Serialize(new ReleaseExpiredHoldPayload(reservationId)),
                Status = BackgroundJobStatus.Pending,
                ScheduledAt = now
            });
            enqueuedCount++;
        }

        if (enqueuedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Enqueued {EnqueuedCount} expired hold release jobs", enqueuedCount);
        }
    }

    private async Task ProcessExpiredHoldJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var holdService = scope.ServiceProvider.GetRequiredService<IReservationHoldService>();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var jobs = await dbContext.BackgroundJobs
            .Where(x =>
                x.Type == BackgroundJobTypes.ReservationHoldReleaseExpired
                && x.Status == BackgroundJobStatus.Pending
                && x.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(x => x.ScheduledAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            return;
        }

        var processedCount = 0;
        foreach (var job in jobs)
        {
            try
            {
                job.Status = BackgroundJobStatus.Processing;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                var payload = JsonSerializer.Deserialize<ReleaseExpiredHoldPayload>(job.Payload)
                    ?? throw new InvalidOperationException("ReleaseExpiredHold payload çözümlenemedi.");

                var reservation = await reservationRepository.GetByIdAsync(payload.ReservationId, cancellationToken);
                if (reservation != null && reservation.Status is ReservationStatus.Hold or ReservationStatus.Draft)
                {
                    reservation.Status = ReservationStatus.Expired;
                    reservation.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await holdService.ReleaseHoldAsync(payload.ReservationId, cancellationToken);

                job.Status = BackgroundJobStatus.Completed;
                job.LastError = null;
                job.FailedAt = null;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                processedCount++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex,
                    "Concurrency conflict while processing expired hold job {BackgroundJobId}",
                    job.Id);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.LastError = ex.Message;
                job.UpdatedAt = DateTime.UtcNow;
                if (job.RetryCount >= HoldReleaseRetryLimit)
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
                logger.LogError(ex, "Release expired hold job {BackgroundJobId} failed", job.Id);
            }
        }

        if (processedCount > 0)
        {
            logger.LogInformation("Processed {ProcessedCount} expired hold release jobs", processedCount);
        }
    }

    private async Task EnsureDailyBackupJobScheduledAsync(CancellationToken cancellationToken)
    {
        if (!dailyBackupOptions.Value.Enabled)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;
        var nextRun = new DateTime(
            now.Year,
            now.Month,
            now.Day,
            Math.Clamp(dailyBackupOptions.Value.ScheduleUtcHour, 0, 23),
            Math.Clamp(dailyBackupOptions.Value.ScheduleUtcMinute, 0, 59),
            0,
            DateTimeKind.Utc);

        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        var hasPendingOrProcessing = await dbContext.BackgroundJobs
            .AsNoTracking()
            .AnyAsync(
                x => x.Type == BackgroundJobTypes.DailyBackupRun
                    && (x.Status == BackgroundJobStatus.Pending || x.Status == BackgroundJobStatus.Processing),
                cancellationToken);

        if (hasPendingOrProcessing)
        {
            return;
        }

        dbContext.BackgroundJobs.Add(new BackgroundJob
        {
            Type = BackgroundJobTypes.DailyBackupRun,
            Payload = JsonSerializer.Serialize(new DailyBackupPayload(nextRun)),
            Status = BackgroundJobStatus.Pending,
            ScheduledAt = nextRun
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Scheduled daily backup job for {ScheduledAtUtc}", nextRun);
    }

    private async Task ProcessDailyBackupJobsAsync(CancellationToken cancellationToken)
    {
        if (!dailyBackupOptions.Value.Enabled)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var jobs = await dbContext.BackgroundJobs
            .Where(x =>
                x.Type == BackgroundJobTypes.DailyBackupRun
                && x.Status == BackgroundJobStatus.Pending
                && x.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(x => x.ScheduledAt)
            .Take(1)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            try
            {
                job.Status = BackgroundJobStatus.Processing;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                await ExecuteDailyBackupCommandAsync(cancellationToken);

                job.Status = BackgroundJobStatus.Completed;
                job.LastError = null;
                job.FailedAt = null;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.LastError = ex.Message;
                job.UpdatedAt = DateTime.UtcNow;

                var retryLimit = Math.Max(dailyBackupOptions.Value.RetryLimit, 1);
                if (job.RetryCount >= retryLimit)
                {
                    job.Status = BackgroundJobStatus.Failed;
                    job.FailedAt = DateTime.UtcNow;
                }
                else
                {
                    job.Status = BackgroundJobStatus.Pending;
                    job.ScheduledAt = DateTime.UtcNow.AddMinutes(15 * job.RetryCount);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogError(ex, "Daily backup job {BackgroundJobId} failed", job.Id);
            }
        }
    }

    private async Task ExecuteDailyBackupCommandAsync(CancellationToken cancellationToken)
    {
        var startInfo = DailyBackupCommandPolicy.CreateStartInfo(dailyBackupOptions.Value);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Daily backup process could not be started.");
        }

        var timeoutSeconds = Math.Max(dailyBackupOptions.Value.TimeoutSeconds, 30);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }

            throw new TimeoutException($"Daily backup process timed out after {timeoutSeconds} seconds.");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Daily backup command failed with exit code {process.ExitCode}. stderr: {DailyBackupCommandPolicy.SanitizeForLog(stderr)}");
        }

        logger.LogInformation(
            "Daily backup command completed successfully. stdout: {Output}",
            DailyBackupCommandPolicy.SanitizeForLog(stdout));
    }
}

public sealed class DailyBackupOptions
{
    public bool Enabled { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> AllowedCommands { get; set; } = [];
    public int ScheduleUtcHour { get; set; } = 2;
    public int ScheduleUtcMinute { get; set; }
    public int TimeoutSeconds { get; set; } = 900;
    public int RetryLimit { get; set; } = 3;
}

internal sealed record ReleaseExpiredHoldPayload(Guid ReservationId);
internal sealed record DailyBackupPayload(DateTime ScheduledForUtc);
