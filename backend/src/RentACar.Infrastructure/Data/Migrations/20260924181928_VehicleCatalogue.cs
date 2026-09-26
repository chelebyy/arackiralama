using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "body_type",
                table: "vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "door_count",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "engine",
                table: "vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "equipment",
                table: "vehicles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "fuel_type",
                table: "vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "luggage_capacity",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "photo_urls",
                table: "vehicles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "power_hp",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seat_count",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transmission",
                table: "vehicles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "body_type",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "door_count",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "engine",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "equipment",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "fuel_type",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "luggage_capacity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "photo_urls",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "power_hp",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "seat_count",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "transmission",
                table: "vehicles");
        }
    }
}
