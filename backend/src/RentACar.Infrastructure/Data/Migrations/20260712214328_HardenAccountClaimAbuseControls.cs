using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenAccountClaimAbuseControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_customer_account_claim_tokens_customer",
                table: "customer_account_claim_tokens");

            migrationBuilder.Sql(
                """
                WITH ranked_tokens AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY customer_id
                               ORDER BY created_at DESC, id DESC) AS row_number
                    FROM customer_account_claim_tokens
                    WHERE consumed_at_utc IS NULL
                      AND superseded_at_utc IS NULL
                )
                UPDATE customer_account_claim_tokens AS token
                SET superseded_at_utc = NOW(),
                    updated_at = NOW()
                FROM ranked_tokens
                WHERE token.id = ranked_tokens.id
                  AND ranked_tokens.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_customer_account_claim_tokens_one_active",
                table: "customer_account_claim_tokens",
                column: "customer_id",
                unique: true,
                filter: "consumed_at_utc IS NULL AND superseded_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_customer_account_claim_tokens_one_active",
                table: "customer_account_claim_tokens");

            migrationBuilder.CreateIndex(
                name: "idx_customer_account_claim_tokens_customer",
                table: "customer_account_claim_tokens",
                column: "customer_id");
        }
    }
}
