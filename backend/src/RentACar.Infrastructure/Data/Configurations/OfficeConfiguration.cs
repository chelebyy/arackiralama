using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.ToTable("offices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(250).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        builder.Property(x => x.IsAirport).HasColumnName("is_airport");
        builder.Property(x => x.OpeningHours).HasColumnName("opening_hours").HasMaxLength(120).IsRequired();

        builder.HasData(
            new Office
            {
                Id = SeedDataConstants.AlanyaCenterOfficeId,
                Name = "Alanya Merkez",
                Address = "Sekerhane Mah. Ataturk Blv. No:10 Alanya/Antalya",
                Phone = "+90 242 000 00 01",
                IsAirport = false,
                OpeningHours = "08:00-22:00",
                CreatedAt = SeedDataConstants.SeededAtUtc,
                UpdatedAt = SeedDataConstants.SeededAtUtc
            },
            new Office
            {
                Id = SeedDataConstants.GazipasaAirportOfficeId,
                Name = "Gazipasa Airport",
                Address = "Gazipasa-Alanya Havalimani Terminal Ici",
                Phone = "+90 242 000 00 02",
                IsAirport = true,
                OpeningHours = "24/7",
                CreatedAt = SeedDataConstants.SeededAtUtc,
                UpdatedAt = SeedDataConstants.SeededAtUtc
            });
    }
}
