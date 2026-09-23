using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;
using RentACar.Core.Enums;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationExtraOptionConfiguration : IEntityTypeConfiguration<ReservationExtraOption>
{
    public void Configure(EntityTypeBuilder<ReservationExtraOption> builder)
    {
        builder.ToTable("reservation_extra_options", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_reservation_extra_options_unit_price", "unit_price >= 0 AND unit_price <= 1000000");
            tableBuilder.HasCheckConstraint("ck_reservation_extra_options_max_quantity", "max_quantity >= 1 AND max_quantity <= 20");
            tableBuilder.HasCheckConstraint("ck_reservation_extra_options_sort_order", "sort_order >= 0 AND sort_order <= 9999");
            tableBuilder.HasCheckConstraint("ck_reservation_extra_options_icon_key", "icon_key IN ('baby', 'users', 'navigation', 'wifi')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
        builder.Property(x => x.PricingMode)
            .HasColumnName("pricing_mode")
            .HasConversion(
                value => value == ReservationExtraPricingMode.PerDay ? "PER_DAY" : "PER_RENTAL",
                value => value == "PER_DAY" ? ReservationExtraPricingMode.PerDay : ReservationExtraPricingMode.PerRental)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.MaxQuantity).HasColumnName("max_quantity");
        builder.Property(x => x.IconKey).HasColumnName("icon_key").HasMaxLength(40).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.IsArchived).HasColumnName("is_archived");
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.IsArchived, x.SortOrder })
            .HasDatabaseName("idx_reservation_extra_options_catalog");

        builder.HasData(
            CreateOption(SeedDataConstants.ChildSeatExtraOptionId, "child_seat", 75m, ReservationExtraPricingMode.PerDay, 20, "baby", 10),
            CreateOption(SeedDataConstants.AdditionalDriverExtraOptionId, "additional_driver", 150m, ReservationExtraPricingMode.PerRental, 20, "users", 20),
            CreateOption(SeedDataConstants.GpsExtraOptionId, "gps", 8m, ReservationExtraPricingMode.PerDay, 1, "navigation", 30),
            CreateOption(SeedDataConstants.WifiExtraOptionId, "wifi", 12m, ReservationExtraPricingMode.PerDay, 1, "wifi", 40));
    }

    private static ReservationExtraOption CreateOption(
        Guid id,
        string code,
        decimal unitPrice,
        ReservationExtraPricingMode pricingMode,
        int maxQuantity,
        string iconKey,
        int sortOrder) => new()
        {
            Id = id,
            Code = code,
            UnitPrice = unitPrice,
            PricingMode = pricingMode,
            MaxQuantity = maxQuantity,
            IconKey = iconKey,
            SortOrder = sortOrder,
            IsActive = false,
            IsArchived = false,
            CreatedAt = SeedDataConstants.SeededAtUtc,
            UpdatedAt = SeedDataConstants.SeededAtUtc
        };
}
