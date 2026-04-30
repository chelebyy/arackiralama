using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogDetailColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "failed_at",
                table: "background_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "background_jobs",
                type: "text",
                nullable: true);

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

            migrationBuilder.InsertData(
                table: "feature_flags",
                columns: new[] { "id", "created_at", "description", "enabled", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "SMS bildirimlerinin gönderimini etkinleştirir", true, "EnableSmsNotifications", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("33333333-3333-3333-3333-333333333334"), new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Arabic (RTL) dil desteğini etkinleştirir", true, "EnableArabicLanguage", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("33333333-3333-3333-3333-333333333335"), new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Sistemi bakım moduna alır", false, "MaintenanceMode", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "feature_flags",
                keyColumn: "id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                table: "feature_flags",
                keyColumn: "id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333334"));

            migrationBuilder.DeleteData(
                table: "feature_flags",
                keyColumn: "id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333335"));

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "background_jobs");

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
