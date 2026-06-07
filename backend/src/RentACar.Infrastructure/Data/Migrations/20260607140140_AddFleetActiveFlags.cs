using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetActiveFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "vehicle_groups",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "offices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "offices",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "offices",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111112"),
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "vehicle_groups",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222221"),
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "vehicle_groups",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "is_active",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "vehicle_groups");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "offices");
        }
    }
}
