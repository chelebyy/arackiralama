using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class GuestAccessConfiguration : IEntityTypeConfiguration<GuestReservationAccess>
{
    public void Configure(EntityTypeBuilder<GuestReservationAccess> builder)
    {
        builder.ToTable("guest_reservation_access");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmailHash).HasMaxLength(64);
        builder.Property(x => x.CodeHash).HasMaxLength(64);
        builder.Property(x => x.SessionHash).HasMaxLength(64);
        builder.Property(x => x.CsrfHash).HasMaxLength(64);
        builder.Property(x => x.Locale).HasMaxLength(5);
        builder.HasIndex(x => new { x.ReservationId, x.CreatedAt });
        builder.HasIndex(x => x.SessionHash).IsUnique();
        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReservationAmendmentConfiguration : IEntityTypeConfiguration<ReservationAmendment>
{
    public void Configure(EntityTypeBuilder<ReservationAmendment> builder)
    {
        builder.ToTable("reservation_amendments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PolicyHash).HasMaxLength(64);
        builder.HasIndex(x => x.QuoteId).IsUnique();
        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GuestReservationAccess>().WithMany().HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
    }
}

