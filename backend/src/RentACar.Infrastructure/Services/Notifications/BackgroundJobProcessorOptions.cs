namespace RentACar.Infrastructure.Services.Notifications;

/// <summary>
/// Configuration for the <see cref="NotificationBackgroundJobProcessor"/> batch processing.
/// Bound to the <c>BackgroundJobs:Processor</c> config section.
/// </summary>
public sealed class BackgroundJobProcessorOptions
{
    public const string SectionName = "BackgroundJobs:Processor";
    public const int DefaultBatchSize = 20;

    public int BatchSize { get; init; } = DefaultBatchSize;
}
