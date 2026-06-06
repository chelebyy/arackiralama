using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RentACar.Infrastructure.Data;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    [DbContext(typeof(RentACarDbContext))]
    [Migration("20260606225000_AddPublicSiteSettingsPages")]
    public partial class AddPublicSiteSettingsPages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS pages_json jsonb NOT NULL DEFAULT '[]'::jsonb;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pages_json",
                table: "public_site_settings");
        }
    }
}
