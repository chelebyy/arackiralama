using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    public partial class GuestReservationManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guest_reservation_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CsrfHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SessionExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    Locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_reservation_access", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guest_reservation_access_reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reservation_amendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    PolicyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Fee = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviousSnapshot = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_amendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reservation_amendments_guest_reservation_access_AccessId",
                        column: x => x.AccessId,
                        principalTable: "guest_reservation_access",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservation_amendments_reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_reservation_access_ReservationId_CreatedAt",
                table: "guest_reservation_access",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_reservation_access_SessionHash",
                table: "guest_reservation_access",
                column: "SessionHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_amendments_AccessId",
                table: "reservation_amendments",
                column: "AccessId");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_amendments_QuoteId",
                table: "reservation_amendments",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_amendments_ReservationId",
                table: "reservation_amendments",
                column: "ReservationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_amendments");

            migrationBuilder.DropTable(
                name: "guest_reservation_access");
        }
    }
}
