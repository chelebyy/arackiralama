using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationHoldConfiguration : IEntityTypeConfiguration<ReservationHold>
{
    public void Configure(EntityTypeBuilder<ReservationHold> builder)
    {
        builder.ToTable("reservation_holds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_holds_expires");
        builder.HasIndex(x => x.SessionId).HasDatabaseName("idx_holds_session");

        builder.HasOne(x => x.Reservation)
            .WithMany(x => x.Holds)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
