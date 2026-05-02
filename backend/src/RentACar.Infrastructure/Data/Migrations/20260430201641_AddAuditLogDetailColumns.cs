using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogDetailColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: failed_at and last_error columns are already added by Phase7 migration.
            // Adding them here would cause duplicate column errors if Phase7 was already applied.
            // Only add audit-specific columns.

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "audit_logs",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "new_value",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_value",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            // Note: EnableSmsNotifications and EnableArabicLanguage already seeded by Phase7.
            // Only insert MaintenanceMode here (not in Phase7).
            migrationBuilder.InsertData(
                table: "feature_flags",
                columns: new[] { "id", "created_at", "description", "enabled", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333335"), new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Sistemi bakım moduna alır", false, "MaintenanceMode", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only delete MaintenanceMode (added in Up above — Phase7 owns EnableSmsNotifications/EnableArabicLanguage).
            migrationBuilder.DeleteData(
                table: "feature_flags",
                keyColumn: "id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333335"));

            // Note: failed_at and last_error columns are NOT dropped here — Phase7 owns them.
            // Only drop audit-specific columns added in Up above.

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "new_value",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "old_value",
                table: "audit_logs");
        }
    }
}
