using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS public_site_settings (
                    id uuid NOT NULL,
                    key character varying(80) NOT NULL,
                    company_name character varying(160) NOT NULL,
                    company_address character varying(500) NOT NULL,
                    company_phone character varying(80) NOT NULL,
                    company_email character varying(160) NOT NULL,
                    working_hours character varying(160) NOT NULL,
                    header_links_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    hero_links_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    quick_links_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    social_links_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    footer_bottom_links_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    contact_page_channels_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    contact_page_offices_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    contact_page_working_hours_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    contact_page_map_title character varying(160) NOT NULL DEFAULT 'Office Locations Map',
                    contact_page_map_embed_url character varying(1200) NOT NULL DEFAULT 'https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d128084.037171682!2d31.95928245!3d36.54115!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x14dca27b8223b0b7%3A0x403b37d0ec0cb80!2sAlanya%2C%20Antalya%2C%20Turkey!5e0!3m2!1sen!2sus!4v1700000000000!5m2!1sen!2sus',
                    contact_page_map_is_visible boolean NOT NULL DEFAULT TRUE,
                    pages_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_public_site_settings" PRIMARY KEY (id)
                );

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS header_links_json jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS hero_links_json jsonb NOT NULL DEFAULT '[]'::jsonb;

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

                ALTER TABLE public_site_settings
                    ADD COLUMN IF NOT EXISTS pages_json jsonb NOT NULL DEFAULT '[]'::jsonb;

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_public_site_settings_key"
                    ON public_site_settings (key);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "public_site_settings");
        }
    }
}
