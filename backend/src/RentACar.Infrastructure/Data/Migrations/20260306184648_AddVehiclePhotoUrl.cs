using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclePhotoUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "vehicles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "features",
                table: "vehicle_groups",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "vehicle_groups",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222221"),
                column: "features",
                value: "[\"AirConditioning\",\"AutomaticTransmission\"]");

            migrationBuilder.UpdateData(
                table: "vehicle_groups",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "features",
                value: "[\"AirConditioning\",\"AutomaticTransmission\",\"BackupCamera\"]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "features",
                table: "vehicle_groups");
        }
    }
}
