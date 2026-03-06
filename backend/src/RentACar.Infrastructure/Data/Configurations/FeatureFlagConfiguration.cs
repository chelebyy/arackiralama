using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Enabled).HasColumnName("enabled");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(
            new FeatureFlag
            {
                Id = SeedDataConstants.OnlinePaymentFeatureFlagId,
                Name = "EnableOnlinePayment",
                Enabled = false,
                Description = "Online payment provider integration toggle",
                CreatedAt = SeedDataConstants.SeededAtUtc,
                UpdatedAt = SeedDataConstants.SeededAtUtc
            },
            new FeatureFlag
            {
                Id = SeedDataConstants.CampaignsFeatureFlagId,
                Name = "EnableCampaigns",
                Enabled = true,
                Description = "Campaign and discount rules toggle",
                CreatedAt = SeedDataConstants.SeededAtUtc,
                UpdatedAt = SeedDataConstants.SeededAtUtc
            });
    }
}
