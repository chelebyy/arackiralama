using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.DiscountType).HasColumnName("discount_type").HasMaxLength(32).IsRequired();
        builder.Property(x => x.DiscountValue).HasColumnName("discount_value").HasPrecision(18, 2);
        builder.Property(x => x.MinDays).HasColumnName("min_days");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder
            .Property(x => x.AllowedVehicleGroupIds)
            .HasColumnName("allowed_vehicle_group_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                vehicleGroupIds => JsonSerializer.Serialize(vehicleGroupIds, (JsonSerializerOptions?)null),
                json => string.IsNullOrWhiteSpace(json)
                    ? new List<Guid>()
                    : JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                (left, right) => left!.SequenceEqual(right!),
                vehicleGroupIds => vehicleGroupIds.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
                vehicleGroupIds => vehicleGroupIds.ToList()));

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
