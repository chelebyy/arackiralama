using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAccountClaimTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_account_claim_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    superseded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issued_from_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    issued_user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_account_claim_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_customer_account_claim_tokens_active_lookup",
                table: "customer_account_claim_tokens",
                columns: new[] { "customer_id", "superseded_at_utc", "consumed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "idx_customer_account_claim_tokens_customer",
                table: "customer_account_claim_tokens",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_customer_account_claim_tokens_expires_at_utc",
                table: "customer_account_claim_tokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_customer_account_claim_tokens_token_hash",
                table: "customer_account_claim_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_account_claim_tokens");
        }
    }
}
