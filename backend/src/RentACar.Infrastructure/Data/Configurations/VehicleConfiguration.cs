using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;
using RentACar.Core.Enums;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Plate).HasColumnName("plate").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Year).HasColumnName("year");
        builder.Property(x => x.Color).HasColumnName("color").HasMaxLength(40).IsRequired();
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.OfficeId).HasColumnName("office_id");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(x => x.Plate).IsUnique();
        builder.HasIndex(x => new { x.OfficeId, x.Status, x.GroupId }).HasDatabaseName("idx_vehicles_office_status_group");
        builder.HasIndex(x => new { x.OfficeId, x.GroupId, x.Status })
            .HasDatabaseName("idx_vehicles_available")
            .HasFilter($"status = '{VehicleStatus.Available}'");

        builder.HasOne(x => x.Group)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Office)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
