using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RentACar.Infrastructure.Data;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(RentACarDbContext))]
    [Migration("20260606182600_AddPublicSiteSettingsContactPageFields")]
    public partial class AddPublicSiteSettingsContactPageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS contact_page_channels_json jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS contact_page_offices_json jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS contact_page_working_hours_json jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS contact_page_map_title character varying(160) NOT NULL DEFAULT 'Office Locations Map';

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS contact_page_map_embed_url character varying(1200) NOT NULL DEFAULT 'https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d128084.037171682!2d31.95928245!3d36.54115!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x14dca27b8223b0b7%3A0x403b37d0ec0cb80!2sAlanya%2C%20Antalya%2C%20Turkey!5e0!3m2!1sen!2sus!4v1700000000000!5m2!1sen!2sus';

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS contact_page_map_is_visible boolean NOT NULL DEFAULT TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact_page_channels_json",
                table: "public_site_settings");

            migrationBuilder.DropColumn(
                name: "contact_page_offices_json",
                table: "public_site_settings");

            migrationBuilder.DropColumn(
                name: "contact_page_working_hours_json",
                table: "public_site_settings");

            migrationBuilder.DropColumn(
                name: "contact_page_map_title",
                table: "public_site_settings");

            migrationBuilder.DropColumn(
                name: "contact_page_map_embed_url",
                table: "public_site_settings");

            migrationBuilder.DropColumn(
                name: "contact_page_map_is_visible",
                table: "public_site_settings");
        }
    }
}
