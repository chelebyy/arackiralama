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
        builder.Property(x => x.PickupOfficeId).HasColumnName("pickup_office_id");
        builder.Property(x => x.ReturnOfficeId).HasColumnName("return_office_id");
        builder.Property(x => x.PickupDateTime).HasColumnName("pickup_datetime");
        builder.Property(x => x.ReturnDateTime).HasColumnName("return_datetime");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(x => x.UnpaidRequestExpiresAtUtc).HasColumnName("unpaid_request_expires_at_utc");
        builder.Property(x => x.DriverFirstName).HasColumnName("driver_first_name").HasMaxLength(100);
        builder.Property(x => x.DriverLastName).HasColumnName("driver_last_name").HasMaxLength(100);
        builder.Property(x => x.DriverDateOfBirth).HasColumnName("driver_date_of_birth");
        builder.Property(x => x.DriverLicenseNumber).HasColumnName("driver_license_number").HasMaxLength(100);
        builder.Property(x => x.DriverLicenseCountry).HasColumnName("driver_license_country").HasMaxLength(100);
        builder.Property(x => x.DriverLicenseIssueDate).HasColumnName("driver_license_issue_date");
        builder.Property(x => x.DriverLicenseExpiryDate).HasColumnName("driver_license_expiry_date");
        builder.Property(x => x.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.HasIndex(x => x.PublicCode).IsUnique();
        builder.HasIndex(x => new { x.VehicleId, x.PickupDateTime, x.ReturnDateTime })
            .HasDatabaseName("idx_reservations_vehicle_dates");
        builder.HasIndex(x => new { x.VehicleId, x.PickupDateTime, x.ReturnDateTime })
            .HasDatabaseName("idx_reservations_active_dates")
            .HasFilter($"status IN ({ReservationStatusGroups.ToPostgresInFilter(ReservationStatusGroups.StockBlocking)})");
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("idx_reservations_status_created");
        builder.HasIndex(x => x.UnpaidRequestExpiresAtUtc)
            .HasDatabaseName("idx_reservations_unpaid_request_expiry")
            .HasFilter($"status = '{ReservationStatus.UnpaidRequest}' AND unpaid_request_expires_at_utc IS NOT NULL");

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PickupOffice)
            .WithMany()
            .HasForeignKey(x => x.PickupOfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReturnOffice)
            .WithMany()
            .HasForeignKey(x => x.ReturnOfficeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
