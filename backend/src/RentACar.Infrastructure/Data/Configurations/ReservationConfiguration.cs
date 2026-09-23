using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using System.Text.Json;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    private static readonly JsonSerializerOptions SnapshotSerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.PublicCode).HasColumnName("public_code").HasMaxLength(Reservation.PublicCodeMaxLength).IsRequired();
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
        builder.Property(x => x.QuoteId).HasColumnName("quote_id");
        var pricingSnapshotProperty = builder.Property(x => x.PricingSnapshot)
            .HasColumnName("pricing_snapshot")
            .HasColumnType("jsonb")
            .HasConversion(
                snapshot => SerializeSnapshot(snapshot),
                json => DeserializeSnapshot(json));
        pricingSnapshotProperty.Metadata.SetValueComparer(new ValueComparer<ReservationPricingSnapshotV1?>(
            (left, right) => SerializeSnapshot(left) == SerializeSnapshot(right),
            snapshot => snapshot == null ? 0 : SerializeSnapshot(snapshot)!.GetHashCode(StringComparison.Ordinal),
            snapshot => DeserializeSnapshot(SerializeSnapshot(snapshot))));
        var quoteReplayProofProperty = builder.Property(x => x.QuoteReplayProof)
            .HasColumnName("quote_replay_proof")
            .HasColumnType("jsonb")
            .HasConversion(
                proof => SerializeReplayProof(proof),
                json => DeserializeReplayProof(json));
        quoteReplayProofProperty.Metadata.SetValueComparer(new ValueComparer<ReservationQuoteReplayProofV1?>(
            (left, right) => SerializeReplayProof(left) == SerializeReplayProof(right),
            proof => proof == null ? 0 : SerializeReplayProof(proof)!.GetHashCode(StringComparison.Ordinal),
            proof => DeserializeReplayProof(SerializeReplayProof(proof))));
        builder.Property(x => x.Version)
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.HasIndex(x => x.PublicCode).IsUnique();
        builder.HasIndex(x => x.QuoteId)
            .IsUnique()
            .HasFilter("quote_id IS NOT NULL");
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

    private static string? SerializeSnapshot(ReservationPricingSnapshotV1? snapshot) =>
        snapshot == null ? null : JsonSerializer.Serialize(snapshot, SnapshotSerializerOptions);

    private static ReservationPricingSnapshotV1? DeserializeSnapshot(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ReservationPricingSnapshotV1>(json, SnapshotSerializerOptions);

    private static string? SerializeReplayProof(ReservationQuoteReplayProofV1? proof) =>
        proof == null ? null : JsonSerializer.Serialize(proof, SnapshotSerializerOptions);

    private static ReservationQuoteReplayProofV1? DeserializeReplayProof(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ReservationQuoteReplayProofV1>(json, SnapshotSerializerOptions);
}
