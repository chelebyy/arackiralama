using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    public partial class IsolateGuestReservationContacts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id", table: "reservations", type: "uuid", nullable: true,
                oldClrType: typeof(Guid), oldType: "uuid");
            migrationBuilder.AddColumn<string>(name: "guest_email", table: "reservations", type: "character varying(160)", maxLength: 160, nullable: true);
            migrationBuilder.AddColumn<string>(name: "guest_full_name", table: "reservations", type: "character varying(140)", maxLength: 140, nullable: true);
            migrationBuilder.AddColumn<string>(name: "guest_identity_number", table: "reservations", type: "character varying(32)", maxLength: 32, nullable: true);
            migrationBuilder.AddColumn<string>(name: "guest_locale", table: "reservations", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<string>(name: "guest_phone", table: "reservations", type: "character varying(40)", maxLength: 40, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM reservations WHERE customer_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot remove guest contacts while ownerless reservations exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id", table: "reservations", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.DropColumn(name: "guest_email", table: "reservations");
            migrationBuilder.DropColumn(name: "guest_full_name", table: "reservations");
            migrationBuilder.DropColumn(name: "guest_identity_number", table: "reservations");
            migrationBuilder.DropColumn(name: "guest_locale", table: "reservations");
            migrationBuilder.DropColumn(name: "guest_phone", table: "reservations");
        }
    }
}
