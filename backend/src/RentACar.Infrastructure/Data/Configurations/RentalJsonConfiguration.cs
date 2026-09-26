using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentACar.Core.Entities;

namespace RentACar.Infrastructure.Data.Configurations;

public sealed class VehicleRentalTermsConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder) =>
        RentalJsonConfiguration.Configure(builder.Property(vehicle => vehicle.RentalTerms), "rental_terms");
}

public sealed class OfficeOperatingPolicyConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder) =>
        RentalJsonConfiguration.Configure(builder.Property(office => office.OperatingPolicy), "operating_policy");
}

internal static class RentalJsonConfiguration
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static void Configure<T>(PropertyBuilder<T?> property, string column) where T : class
    {
        property.HasColumnName(column).HasColumnType("jsonb")
            .HasConversion(value => Serialize(value), json => Deserialize<T>(json));
        property.Metadata.SetValueComparer(new ValueComparer<T?>(
            (left, right) => Serialize(left) == Serialize(right),
            value => value == null ? 0 : Serialize(value)!.GetHashCode(StringComparison.Ordinal),
            value => Deserialize<T>(Serialize(value))));
    }

    private static string? Serialize<T>(T? value) where T : class =>
        value is null ? null : JsonSerializer.Serialize(value, Options);

    private static T? Deserialize<T>(string? value) where T : class =>
        value is null ? null : JsonSerializer.Deserialize<T>(value, Options);
}
