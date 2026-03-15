using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6AuthDomainPersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_admin_users_email",
                table: "admin_users");

            migrationBuilder.RenameIndex(
                name: "IX_customers_identity_number",
                table: "customers",
                newName: "idx_customers_identity_number");

            migrationBuilder.RenameIndex(
                name: "IX_customers_email",
                table: "customers",
                newName: "idx_customers_email");

            migrationBuilder.AddColumn<int>(
                name: "failed_login_count",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at_utc",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_end_utc",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                table: "customers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "failed_login_count",
                table: "admin_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at_utc",
                table: "admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_end_utc",
                table: "admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                table: "admin_users",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "admin_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Safe rollout note:
            // Existing guest-created rows can predate normalized-email uniqueness. Backfill first,
            // then reject duplicates with actionable diagnostics before adding unique constraints.
            migrationBuilder.Sql("""
                UPDATE customers
                SET normalized_email = UPPER(TRIM(email))
                WHERE normalized_email IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE admin_users
                SET normalized_email = UPPER(TRIM(email))
                WHERE normalized_email IS NULL;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT normalized_email
                            FROM customers
                            GROUP BY normalized_email
                            HAVING COUNT(*) > 1
                        ) duplicate_customers
                    ) THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'Duplicate normalized customer emails detected.',
                            DETAIL = 'Resolve duplicate guest/customer rows before applying ux_customers_normalized_email.',
                            HINT = 'Run: SELECT UPPER(TRIM(email)) AS normalized_email, array_agg(id) AS customer_ids FROM customers GROUP BY UPPER(TRIM(email)) HAVING COUNT(*) > 1;';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM (
                            SELECT normalized_email
                            FROM admin_users
                            GROUP BY normalized_email
                            HAVING COUNT(*) > 1
                        ) duplicate_admins
                    ) THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'Duplicate normalized admin emails detected.',
                            DETAIL = 'Resolve duplicate admin rows before applying ux_admin_users_normalized_email.',
                            HINT = 'Run: SELECT UPPER(TRIM(email)) AS normalized_email, array_agg(id) AS admin_ids FROM admin_users GROUP BY UPPER(TRIM(email)) HAVING COUNT(*) > 1;';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "customers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "admin_users",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    refresh_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_customers_normalized_email",
                table: "customers",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_admin_users_email",
                table: "admin_users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ux_admin_users_normalized_email",
                table: "admin_users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_auth_sessions_principal",
                table: "auth_sessions",
                columns: new[] { "principal_type", "principal_id" });

            migrationBuilder.CreateIndex(
                name: "idx_auth_sessions_refresh_token_expires_at_utc",
                table: "auth_sessions",
                column: "refresh_token_expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_auth_sessions_revoked_at_utc",
                table: "auth_sessions",
                column: "revoked_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_auth_sessions_refresh_token_hash",
                table: "auth_sessions",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_password_reset_tokens_consumed_at_utc",
                table: "password_reset_tokens",
                column: "consumed_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_password_reset_tokens_expires_at_utc",
                table: "password_reset_tokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "idx_password_reset_tokens_principal",
                table: "password_reset_tokens",
                columns: new[] { "principal_type", "principal_id" });

            migrationBuilder.CreateIndex(
                name: "ux_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropIndex(
                name: "ux_customers_normalized_email",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "idx_admin_users_email",
                table: "admin_users");

            migrationBuilder.DropIndex(
                name: "ux_admin_users_normalized_email",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "failed_login_count",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "last_login_at_utc",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "lockout_end_utc",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "normalized_email",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "failed_login_count",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "last_login_at_utc",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "lockout_end_utc",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "normalized_email",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "admin_users");

            migrationBuilder.RenameIndex(
                name: "idx_customers_identity_number",
                table: "customers",
                newName: "IX_customers_identity_number");

            migrationBuilder.RenameIndex(
                name: "idx_customers_email",
                table: "customers",
                newName: "IX_customers_email");

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_email",
                table: "admin_users",
                column: "email",
                unique: true);
        }
    }
}
