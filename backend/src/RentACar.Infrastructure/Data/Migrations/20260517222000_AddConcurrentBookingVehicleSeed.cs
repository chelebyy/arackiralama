using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations;

[DbContext(typeof(RentACarDbContext))]
[Migration("20260517222000_AddConcurrentBookingVehicleSeed")]
public partial class AddConcurrentBookingVehicleSeed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Synthetic concurrent-booking inventory is seeded at API startup in local load-test mode.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op: the local startup seed is idempotent and intentionally excluded from the shared migration chain.
    }
}
