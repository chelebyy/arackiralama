using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data;

// Data/Configurations dizinine yazma sorunu nedeniyle eksik seed kayıtları burada tutuluyor.
public sealed class FeatureFlagSeedExtensionsConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    private static readonly DateTime SeededAtUtc = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.HasData(
            new FeatureFlag
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "EnableSmsNotifications",
                Enabled = true,
                Description = "SMS bildirimlerinin gönderimini etkinleştirir",
                CreatedAt = SeededAtUtc,
                UpdatedAt = SeededAtUtc
            },
            new FeatureFlag
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333334"),
                Name = "EnableArabicLanguage",
                Enabled = true,
                Description = "Arabic (RTL) dil desteğini etkinleştirir",
                CreatedAt = SeededAtUtc,
                UpdatedAt = SeededAtUtc
            },
            new FeatureFlag
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333335"),
                Name = "MaintenanceMode",
                Enabled = false,
                Description = "Sistemi bakım moduna alır",
                CreatedAt = SeededAtUtc,
                UpdatedAt = SeededAtUtc
            });
    }
}
