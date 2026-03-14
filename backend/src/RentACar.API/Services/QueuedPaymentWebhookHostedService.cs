using Microsoft.Extensions.DependencyInjection;

namespace RentACar.API.Services;

public sealed class QueuedPaymentWebhookHostedService(
    IServiceProvider serviceProvider,
    ILogger<QueuedPaymentWebhookHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();
                var processedCount = await paymentService.ProcessPendingWebhookJobsAsync(stoppingToken);

                if (processedCount > 0)
                {
                    logger.LogInformation("Processed {ProcessedCount} queued payment webhook jobs.", processedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process queued payment webhook jobs.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
