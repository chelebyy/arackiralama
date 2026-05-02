using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260502000000_SyncPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_intents_reservation_id",
                table: "payment_intents");

            migrationBuilder.AddColumn<string>(
                name: "refund_idempotency_key",
                table: "payment_intents",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_reason",
                table: "payment_intents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_reference_id",
                table: "payment_intents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_amount",
                table: "payment_intents",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "offices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "offices",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "code",
                value: "ala");

            migrationBuilder.UpdateData(
                table: "offices",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111112"),
                column: "code",
                value: "gzp");

            migrationBuilder.CreateIndex(
                name: "idx_payment_reservation_refund_idempotency",
                table: "payment_intents",
                columns: new[] { "reservation_id", "refund_idempotency_key" });

            migrationBuilder.CreateIndex(
                name: "IX_offices_code",
                table: "offices",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_type_payload",
                table: "background_jobs",
                columns: new[] { "type", "payload" },
                unique: true,
                filter: "\"status\" IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_reservation_refund_idempotency",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_offices_code",
                table: "offices");

            migrationBuilder.DropIndex(
                name: "IX_background_jobs_type_payload",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "refund_idempotency_key",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "refund_reason",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "refund_reference_id",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "refunded_amount",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "code",
                table: "offices");

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_reservation_id",
                table: "payment_intents",
                column: "reservation_id");
        }
    }
}
