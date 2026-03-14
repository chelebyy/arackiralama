using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderReferencesToPaymentIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_idempotency",
                table: "payment_intents");

            migrationBuilder.AddColumn<string>(
                name: "provider_intent_id",
                table: "payment_intents",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_transaction_id",
                table: "payment_intents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_provider_idempotency",
                table: "payment_intents",
                columns: new[] { "provider", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_provider_intent_id",
                table: "payment_intents",
                column: "provider_intent_id");

            migrationBuilder.CreateIndex(
                name: "idx_payment_provider_transaction_id",
                table: "payment_intents",
                column: "provider_transaction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_payment_provider_idempotency",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "idx_payment_provider_intent_id",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "idx_payment_provider_transaction_id",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "provider_intent_id",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "provider_transaction_id",
                table: "payment_intents");

            migrationBuilder.CreateIndex(
                name: "idx_payment_idempotency",
                table: "payment_intents",
                column: "idempotency_key",
                unique: true);
        }
    }
}
