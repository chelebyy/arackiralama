using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;
using System.Text.Json;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class VehicleGroupConfiguration : IEntityTypeConfiguration<VehicleGroup>
{
    public void Configure(EntityTypeBuilder<VehicleGroup> builder)
    {
        builder.ToTable("vehicle_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.NameTr).HasColumnName("name_tr").HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameRu).HasColumnName("name_ru").HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameAr).HasColumnName("name_ar").HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameDe).HasColumnName("name_de").HasMaxLength(120).IsRequired();
        builder.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasPrecision(18, 2);
        builder.Property(x => x.MinAge).HasColumnName("min_age");
        builder.Property(x => x.MinLicenseYears).HasColumnName("min_license_years");
        builder
            .Property(x => x.Features)
            .HasColumnName("features")
            .HasColumnType("jsonb")
            .HasConversion(
                features => JsonSerializer.Serialize(features, (JsonSerializerOptions?)null),
                json => string.IsNullOrWhiteSpace(json)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (left, right) => left!.SequenceEqual(right!),
                features => features.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode(StringComparison.Ordinal))),
                features => features.ToList()));

        builder.HasData(
            new VehicleGroup
            {
                Id = SeedDataConstants.EconomyGroupId,
                NameTr = "Ekonomi",
                NameEn = "Economy",
                NameRu = "Economy",
                NameAr = "Economy",
                NameDe = "Economy",
                DepositAmount = 2000m,
                MinAge = 21,
                MinLicenseYears = 2,
                Features = ["AirConditioning", "AutomaticTransmission"],
                CreatedAt = SeedDataConstants.SeededAtUtc,
                UpdatedAt = SeedDataConstants.SeededAtUtc
            },
            new VehicleGroup
            {
                Id = SeedDataConstants.SuvGroupId,
                NameTr = "SUV",
                NameEn = "SUV",
                NameRu = "SUV",
                NameAr = "SUV",
                NameDe = "SUV",
                DepositAmount = 3500m,
                MinAge = 25,
                MinLicenseYears = 3,
                Features = ["AirConditioning", "AutomaticTransmission", "BackupCamera"],
                CreatedAt = SeedDataConstants.SeededAtUtc,
                UpdatedAt = SeedDataConstants.SeededAtUtc
            });
    }
}
