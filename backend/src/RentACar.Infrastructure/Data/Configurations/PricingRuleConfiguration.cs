using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("pricing_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.VehicleGroupId).HasColumnName("vehicle_group_id");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.DailyPrice).HasColumnName("daily_price").HasPrecision(18, 2);
        builder.Property(x => x.Multiplier).HasColumnName("multiplier").HasPrecision(8, 4);
        builder.Property(x => x.WeekdayMultiplier).HasColumnName("weekday_multiplier").HasPrecision(8, 4);
        builder.Property(x => x.WeekendMultiplier).HasColumnName("weekend_multiplier").HasPrecision(8, 4);
        builder.Property(x => x.CalculationType).HasColumnName("calculation_type").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority");

        builder.HasIndex(x => new { x.StartDate, x.EndDate }).HasDatabaseName("idx_pricing_date_range");
        builder.HasIndex(x => new { x.VehicleGroupId, x.Priority, x.StartDate, x.EndDate })
            .HasDatabaseName("idx_pricing_group_priority_range");

        builder.HasOne(x => x.VehicleGroup)
            .WithMany(x => x.PricingRules)
            .HasForeignKey(x => x.VehicleGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
