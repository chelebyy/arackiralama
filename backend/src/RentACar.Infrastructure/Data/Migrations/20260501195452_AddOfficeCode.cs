using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // Backfill any existing offices that don't have a code assigned
            // to prevent unique constraint violation on non-empty databases.
            // Uses the first 8 chars of the GUID as a deterministic unique code.
            migrationBuilder.Sql(
                "UPDATE offices SET code = SUBSTRING(id::text, 1, 8) WHERE code = '' OR code IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_offices_code",
                table: "offices",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_offices_code",
                table: "offices");

            migrationBuilder.DropColumn(
                name: "code",
                table: "offices");
        }
    }
}
