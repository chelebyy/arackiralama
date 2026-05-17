using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Data.Configurations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations;

[DbContext(typeof(RentACarDbContext))]
[Migration("20260517222000_AddConcurrentBookingVehicleSeed")]
public partial class AddConcurrentBookingVehicleSeed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var seededAtUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 1; i <= 120; i++)
        {
            var vehicleId = Guid.Parse($"44444444-4444-4444-4444-{i:D12}");
            var plate = $"34LT{i:000}";
            var brand = i % 2 == 0 ? "Renault" : "Fiat";
            var model = i % 2 == 0 ? "Clio" : "Egea";
            var color = (i % 3) switch
            {
                0 => "White",
                1 => "Gray",
                _ => "Black"
            };
            var year = 2022 + (i % 3);

            migrationBuilder.Sql($"""
                INSERT INTO vehicles (
                    id,
                    brand,
                    color,
                    created_at,
                    group_id,
                    model,
                    office_id,
                    photo_url,
                    plate,
                    status,
                    updated_at,
                    year)
                VALUES (
                    '{vehicleId}',
                    '{brand}',
                    '{color}',
                    '{seededAtUtc:O}',
                    '{SeedDataConstants.EconomyGroupId}',
                    '{model}',
                    '{SeedDataConstants.AlanyaCenterOfficeId}',
                    NULL,
                    '{plate}',
                    'Available',
                    '{seededAtUtc:O}',
                    {year});
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DELETE FROM vehicles
            WHERE office_id = '{SeedDataConstants.AlanyaCenterOfficeId}'
              AND group_id = '{SeedDataConstants.EconomyGroupId}'
              AND plate LIKE '34LT%';
            """);
    }
}
