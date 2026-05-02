namespace RentACar.Infrastructure.Services.Notifications;

public sealed class BackgroundJobProcessorOptions
{
    public const string SectionName = "BackgroundJobs:Processor";
    public const int DefaultBatchSize = 20;

    public int BatchSize { get; init; } = DefaultBatchSize;
}
