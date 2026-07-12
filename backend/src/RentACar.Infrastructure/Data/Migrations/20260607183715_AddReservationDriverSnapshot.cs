using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDriverSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "driver_date_of_birth",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_first_name",
                table: "reservations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_last_name",
                table: "reservations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_license_country",
                table: "reservations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "driver_license_expiry_date",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "driver_license_issue_date",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_license_number",
                table: "reservations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "driver_date_of_birth",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "driver_first_name",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "driver_last_name",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "driver_license_country",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "driver_license_expiry_date",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "driver_license_issue_date",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "driver_license_number",
                table: "reservations");
        }
    }
}
