using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.FeatureFlags;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class FeatureFlagService(IApplicationDbContext dbContext) : IFeatureFlagService
{
    private const string OnlinePaymentDescription =
        "Online ödeme seçeneklerini public rezervasyon akışında gösterir. Kapalıyken müşteriler ödeme yapmadan 24 saat stok bloklu talep oluşturur.";

    private static readonly (string Name, bool Enabled, string Description)[] RequiredFlags =
    [
        ("EnableOnlinePayment", false, OnlinePaymentDescription),
        ("EnableSmsNotifications", true, "SMS bildirimlerinin gönderimini etkinleştirir"),
        ("EnableCampaigns", true, "Campaign and discount rules toggle"),
        ("EnableArabicLanguage", true, "Arabic (RTL) dil desteğini etkinleştirir"),
        ("MaintenanceMode", false, "Sistemi bakım moduna alır")
    ];

    public async Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRequiredFlagsAsync(cancellationToken);

        return await dbContext.FeatureFlags
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new FeatureFlagDto(
                x.Id,
                x.Name,
                x.Enabled,
                x.Description,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FeatureFlagDto?> UpdateAsync(
        string name,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return null;
        }

        var flag = await dbContext.FeatureFlags
            .FirstOrDefaultAsync(
                x => x.Name == trimmedName,
                cancellationToken);

        if (flag is null)
        {
            var template = RequiredFlags.FirstOrDefault(x => x.Name == trimmedName);
            if (string.IsNullOrWhiteSpace(template.Name))
            {
                return null;
            }

            flag = new Core.Entities.FeatureFlag
            {
                Name = template.Name,
                Description = template.Description,
                Enabled = enabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.FeatureFlags.Add(flag);
        }
        else
        {
            flag.Enabled = enabled;
            flag.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new FeatureFlagDto(
            flag.Id,
            flag.Name,
            flag.Enabled,
            flag.Description,
            flag.UpdatedAt);
    }

    private async Task EnsureRequiredFlagsAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.FeatureFlags
            .Where(x => RequiredFlags.Select(flag => flag.Name).Contains(x.Name))
            .ToListAsync(cancellationToken);
        var existingNames = existing.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var missing = RequiredFlags
            .Where(x => !existingNames.Contains(x.Name))
            .Select(x => new Core.Entities.FeatureFlag
            {
                Name = x.Name,
                Enabled = x.Enabled,
                Description = x.Description,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        var descriptionsUpdated = false;
        foreach (var flag in existing)
        {
            var template = RequiredFlags.First(x => x.Name == flag.Name);
            if (!string.Equals(flag.Description, template.Description, StringComparison.Ordinal))
            {
                flag.Description = template.Description;
                flag.UpdatedAt = now;
                descriptionsUpdated = true;
            }
        }

        if (missing.Count == 0 && !descriptionsUpdated)
        {
            return;
        }

        dbContext.FeatureFlags.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
