using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Services;

public sealed class CustomerAccountClaimTokenCleanupService(
    IApplicationDbContext dbContext,
    IOptions<AccountClaimSecurityOptions> options,
    TimeProvider timeProvider)
{
    private readonly AccountClaimSecurityOptions _options = options.Value;

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var retentionCutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-_options.TokenRetentionDays);
        var tokenIds = await dbContext.CustomerAccountClaimTokens
            .AsNoTracking()
            .Where(token =>
                (token.ConsumedAtUtc.HasValue && token.ConsumedAtUtc < retentionCutoff)
                || (token.SupersededAtUtc.HasValue && token.SupersededAtUtc < retentionCutoff)
                || token.ExpiresAtUtc < retentionCutoff)
            .OrderBy(token => token.UpdatedAt)
            .Select(token => token.Id)
            .Take(_options.CleanupBatchSize)
            .ToListAsync(cancellationToken);

        if (tokenIds.Count == 0)
        {
            return 0;
        }

        var tokens = await dbContext.CustomerAccountClaimTokens
            .Where(token => tokenIds.Contains(token.Id))
            .ToListAsync(cancellationToken);
        dbContext.CustomerAccountClaimTokens.RemoveRange(tokens);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }
}
