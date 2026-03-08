using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3PricingEngineCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pricing_rules_vehicle_group_id",
                table: "pricing_rules");

            migrationBuilder.AddColumn<string>(
                name: "calculation_type",
                table: "pricing_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "pricing_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "weekday_multiplier",
                table: "pricing_rules",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "weekend_multiplier",
                table: "pricing_rules",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "allowed_vehicle_group_ids",
                table: "campaigns",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_pricing_group_priority_range",
                table: "pricing_rules",
                columns: new[] { "vehicle_group_id", "priority", "start_date", "end_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_pricing_group_priority_range",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "calculation_type",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "weekday_multiplier",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "weekend_multiplier",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "allowed_vehicle_group_ids",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_vehicle_group_id",
                table: "pricing_rules",
                column: "vehicle_group_id");
        }
    }
}
