using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnpaidRequestAndManualReservationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_reservations_active_dates",
                table: "reservations");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "reservations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pickup_office_id",
                table: "reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "return_office_id",
                table: "reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "unpaid_request_expires_at_utc",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE reservations r
                SET pickup_office_id = v.office_id,
                    return_office_id = v.office_id
                FROM vehicles v
                WHERE r.vehicle_id = v.id
                  AND (r.pickup_office_id = '00000000-0000-0000-0000-000000000000'
                    OR r.return_office_id = '00000000-0000-0000-0000-000000000000');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_pickup_office_id",
                table: "reservations",
                column: "pickup_office_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_return_office_id",
                table: "reservations",
                column: "return_office_id");

            migrationBuilder.CreateIndex(
                name: "idx_reservations_active_dates",
                table: "reservations",
                columns: new[] { "vehicle_id", "pickup_datetime", "return_datetime" },
                filter: "status IN ('Hold','UnpaidRequest','PendingPayment','Paid','Confirmed','Active')");

            migrationBuilder.CreateIndex(
                name: "idx_reservations_unpaid_request_expiry",
                table: "reservations",
                column: "unpaid_request_expires_at_utc",
                filter: "status = 'UnpaidRequest' AND unpaid_request_expires_at_utc IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_offices_pickup_office_id",
                table: "reservations",
                column: "pickup_office_id",
                principalTable: "offices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_offices_return_office_id",
                table: "reservations",
                column: "return_office_id",
                principalTable: "offices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE reservations
                DROP CONSTRAINT IF EXISTS reservations_no_overlap;

                ALTER TABLE reservations
                ADD CONSTRAINT reservations_no_overlap
                EXCLUDE USING gist
                (
                    vehicle_id WITH =,
                    tstzrange(pickup_datetime, return_datetime, '[)') WITH &&
                )
                WHERE (status IN ('Hold', 'UnpaidRequest', 'PendingPayment', 'Paid', 'Confirmed', 'Active'));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE reservations
                ADD CONSTRAINT ck_reservations_unpaid_request_expiry
                CHECK (
                    (status = 'UnpaidRequest' AND unpaid_request_expires_at_utc IS NOT NULL)
                    OR (status <> 'UnpaidRequest' AND unpaid_request_expires_at_utc IS NULL)
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE feature_flags
                SET description = 'Online ödeme seçeneklerini public rezervasyon akışında gösterir. Kapalıyken müşteriler ödeme yapmadan 24 saat stok bloklu talep oluşturur.',
                    updated_at = NOW()
                WHERE name = 'EnableOnlinePayment';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE reservations
                DROP CONSTRAINT IF EXISTS ck_reservations_unpaid_request_expiry;

                ALTER TABLE reservations
                DROP CONSTRAINT IF EXISTS reservations_no_overlap;

                ALTER TABLE reservations
                ADD CONSTRAINT reservations_no_overlap
                EXCLUDE USING gist
                (
                    vehicle_id WITH =,
                    tstzrange(pickup_datetime, return_datetime, '[)') WITH &&
                )
                WHERE (status IN ('Hold', 'PendingPayment', 'Paid', 'Active'));
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_reservations_offices_pickup_office_id",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_reservations_offices_return_office_id",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_pickup_office_id",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_return_office_id",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "idx_reservations_active_dates",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "idx_reservations_unpaid_request_expiry",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "pickup_office_id",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "return_office_id",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "unpaid_request_expires_at_utc",
                table: "reservations");

            migrationBuilder.CreateIndex(
                name: "idx_reservations_active_dates",
                table: "reservations",
                columns: new[] { "vehicle_id", "pickup_datetime", "return_datetime" },
                filter: "status IN ('Paid','Active')");
        }
    }
}
