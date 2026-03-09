using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;
using RentACar.Core.Enums;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.PublicCode).HasColumnName("public_code").HasMaxLength(24).IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.PickupDateTime).HasColumnName("pickup_datetime");
        builder.Property(x => x.ReturnDateTime).HasColumnName("return_datetime");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
        builder.Property(x => x.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.HasIndex(x => x.PublicCode).IsUnique();
        builder.HasIndex(x => new { x.VehicleId, x.PickupDateTime, x.ReturnDateTime })
            .HasDatabaseName("idx_reservations_vehicle_dates");
        builder.HasIndex(x => new { x.VehicleId, x.PickupDateTime, x.ReturnDateTime })
            .HasDatabaseName("idx_reservations_active_dates")
            .HasFilter($"status IN ('{ReservationStatus.Paid}','{ReservationStatus.Active}')");
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("idx_reservations_status_created");

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
