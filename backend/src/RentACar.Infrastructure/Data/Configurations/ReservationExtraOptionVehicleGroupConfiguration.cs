using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class ReservationExtraOptionVehicleGroupConfiguration : IEntityTypeConfiguration<ReservationExtraOptionVehicleGroup>
{
    public void Configure(EntityTypeBuilder<ReservationExtraOptionVehicleGroup> builder)
    {
        builder.ToTable("reservation_extra_option_vehicle_groups");
        builder.HasKey(x => new { x.OptionId, x.VehicleGroupId });
        builder.Property(x => x.OptionId).HasColumnName("option_id");
        builder.Property(x => x.VehicleGroupId).HasColumnName("vehicle_group_id");
        builder.HasIndex(x => x.VehicleGroupId).HasDatabaseName("idx_reservation_extra_option_vehicle_groups_group");

        builder.HasOne(x => x.Option)
            .WithMany(x => x.VehicleGroups)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.VehicleGroup)
            .WithMany(x => x.ReservationExtraOptions)
            .HasForeignKey(x => x.VehicleGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
