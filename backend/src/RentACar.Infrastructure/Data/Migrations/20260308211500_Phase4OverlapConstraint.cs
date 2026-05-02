using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4OverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE reservations
                ADD CONSTRAINT reservations_no_overlap
                EXCLUDE USING gist
                (
                    vehicle_id WITH =,
                    tstzrange(pickup_datetime, return_datetime, '[)') WITH &&
                )
                WHERE (status IN ('Hold', 'PendingPayment', 'Paid', 'Active'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE reservations
                DROP CONSTRAINT IF EXISTS reservations_no_overlap;
                """);

            migrationBuilder.Sql("DROP EXTENSION IF EXISTS btree_gist;");
        }
    }
}
