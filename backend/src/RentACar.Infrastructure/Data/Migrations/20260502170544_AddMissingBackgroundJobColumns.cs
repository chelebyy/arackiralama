using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingBackgroundJobColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'background_jobs'
                          AND column_name = 'last_error') THEN
                        ALTER TABLE background_jobs ADD COLUMN last_error text;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'background_jobs'
                          AND column_name = 'failed_at') THEN
                        ALTER TABLE background_jobs ADD COLUMN failed_at timestamp with time zone;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'background_jobs'
                          AND column_name = 'failed_at') THEN
                        ALTER TABLE background_jobs DROP COLUMN failed_at;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'background_jobs'
                          AND column_name = 'last_error') THEN
                        ALTER TABLE background_jobs DROP COLUMN last_error;
                    END IF;
                END
                $$;
                """);
        }
    }
}
