namespace RentACar.Infrastructure.Services;

public sealed class AccountClaimSecurityOptions
{
    public const string SectionName = "AccountClaimSecurity";

    public int RequestCooldownMinutes { get; init; } = 5;
    public int TokenRetentionDays { get; init; } = 14;
    public int CleanupIntervalMinutes { get; init; } = 60;
    public int CleanupBatchSize { get; init; } = 200;
}
