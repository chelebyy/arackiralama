using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;
using RentACar.Core.Enums;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationSelectedExtraConfiguration : IEntityTypeConfiguration<ReservationSelectedExtra>
{
    public void Configure(EntityTypeBuilder<ReservationSelectedExtra> builder)
    {
        builder.ToTable("reservation_selected_extras", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_reservation_selected_extras_quantity", "quantity >= 1 AND quantity <= 20");
            tableBuilder.HasCheckConstraint("ck_reservation_selected_extras_currency", "currency = 'TRY'");
            tableBuilder.HasCheckConstraint("ck_reservation_selected_extras_prices", "unit_price_snapshot >= 0 AND total_price_snapshot >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.ExtraOptionId).HasColumnName("extra_option_id");
        builder.Property(x => x.OptionVersionSnapshot).HasColumnName("option_version_snapshot");
        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(5).IsRequired();
        builder.Property(x => x.OptionCodeSnapshot).HasColumnName("option_code_snapshot").HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameSnapshot).HasColumnName("name_snapshot").HasMaxLength(100).IsRequired();
        builder.Property(x => x.DescriptionSnapshot).HasColumnName("description_snapshot").HasMaxLength(300).IsRequired();
        builder.Property(x => x.UnitPriceSnapshot).HasColumnName("unit_price_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.PricingModeSnapshot)
            .HasColumnName("pricing_mode_snapshot")
            .HasConversion(
                value => value == ReservationExtraPricingMode.PerDay ? "PER_DAY" : "PER_RENTAL",
                value => value == "PER_DAY" ? ReservationExtraPricingMode.PerDay : ReservationExtraPricingMode.PerRental)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.RentalDaysSnapshot).HasColumnName("rental_days_snapshot");
        builder.Property(x => x.TotalPriceSnapshot).HasColumnName("total_price_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.ReservationId).HasDatabaseName("idx_reservation_selected_extras_reservation");
        builder.HasIndex(x => new { x.ReservationId, x.ExtraOptionId }).IsUnique();
        builder.HasOne(x => x.Reservation)
            .WithMany(x => x.SelectedExtras)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ExtraOption)
            .WithMany(x => x.SelectedExtras)
            .HasForeignKey(x => x.ExtraOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
