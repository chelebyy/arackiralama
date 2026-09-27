using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleRentalPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "group_id",
                table: "vehicles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "rental_terms",
                table: "vehicles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "occupied_until_utc",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operating_policy",
                table: "offices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE vehicles v SET rental_terms = jsonb_build_object(
                    'depositAmount', g.deposit_amount, 'minAge', g.min_age, 'minLicenseYears', g.min_license_years,
                    'rates', COALESCE((SELECT jsonb_agg(jsonb_build_object(
                        'id', p.id, 'startDate', p.start_date, 'endDate', p.end_date,
                        'dailyPrice', p.daily_price, 'multiplier', p.multiplier,
                        'weekdayMultiplier', p.weekday_multiplier, 'weekendMultiplier', p.weekend_multiplier,
                        'calculationType', p.calculation_type, 'priority', p.priority, 'createdAt', p.created_at)
                        ORDER BY p.priority DESC, p.start_date DESC, p.end_date DESC, p.created_at DESC, p.id)
                        FROM pricing_rules p WHERE p.vehicle_group_id = g.id), '[]'::jsonb),
                    'extraOptionIds', COALESCE((SELECT jsonb_agg(e.option_id ORDER BY e.option_id)
                        FROM reservation_extra_option_vehicle_groups e WHERE e.vehicle_group_id = g.id), '[]'::jsonb))
                FROM vehicle_groups g WHERE v.group_id = g.id AND v.rental_terms IS NULL;

                ALTER TABLE reservations DROP CONSTRAINT reservations_no_overlap;
                ALTER TABLE reservations ADD CONSTRAINT reservations_no_overlap EXCLUDE USING gist
                (vehicle_id WITH =, tstzrange(pickup_datetime, COALESCE(occupied_until_utc, return_datetime), '[)') WITH &&)
                WHERE (status IN ('Hold', 'UnpaidRequest', 'PendingPayment', 'Paid', 'Confirmed', 'Active'));
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION reservation_vehicle_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE vehicle_status text;
                BEGIN
                    IF NEW.status IN ('Hold', 'UnpaidRequest', 'PendingPayment', 'Paid', 'Confirmed', 'Active') THEN
                        SELECT status INTO vehicle_status FROM vehicles WHERE id = NEW.vehicle_id FOR UPDATE;
                        IF NEW.pricing_snapshot->'bookingConditions' IS NOT NULL AND NEW.pricing_snapshot->'bookingConditions' <> 'null'::jsonb THEN
                            NEW.occupied_until_utc := NEW.return_datetime + make_interval(mins =>
                                COALESCE((NEW.pricing_snapshot->'bookingConditions'->>'preparationMinutes')::int, 0));
                            IF vehicle_status <> 'Available' AND (TG_OP = 'INSERT' OR NEW.vehicle_id <> OLD.vehicle_id
                                OR OLD.status NOT IN ('Hold', 'UnpaidRequest', 'PendingPayment', 'Paid', 'Confirmed', 'Active')) THEN
                                RAISE EXCEPTION 'Selected vehicle is unavailable' USING ERRCODE = '23P01', CONSTRAINT = 'reservations_no_overlap';
                            END IF;
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reservation_vehicle_guard BEFORE INSERT OR UPDATE ON reservations
                FOR EACH ROW EXECUTE FUNCTION reservation_vehicle_guard();

                CREATE FUNCTION vehicle_allocation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.office_id <> OLD.office_id OR (NEW.status IN ('Maintenance', 'OutOfService', 'Retired') AND NEW.status <> OLD.status))
                        AND EXISTS (SELECT 1 FROM reservations r WHERE r.vehicle_id = NEW.id
                            AND r.status IN ('Hold', 'UnpaidRequest', 'PendingPayment', 'Paid', 'Confirmed', 'Active')
                            AND COALESCE(r.occupied_until_utc, r.return_datetime) > CURRENT_TIMESTAMP) THEN
                        RAISE EXCEPTION 'Vehicle has an active allocation' USING ERRCODE = '23P01', CONSTRAINT = 'reservations_no_overlap';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER vehicle_allocation_guard BEFORE UPDATE ON vehicles
                FOR EACH ROW EXECUTE FUNCTION vehicle_allocation_guard();
                """);

            migrationBuilder.UpdateData(
                table: "offices",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "operating_policy",
                value: null);

            migrationBuilder.UpdateData(
                table: "offices",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111112"),
                column: "operating_policy",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM vehicles WHERE group_id IS NULL) THEN
                        RAISE EXCEPTION 'Assign legacy groups before reverting vehicle rental policies';
                    END IF;
                END $$;
                DROP TRIGGER vehicle_allocation_guard ON vehicles;
                DROP FUNCTION vehicle_allocation_guard();
                DROP TRIGGER reservation_vehicle_guard ON reservations;
                DROP FUNCTION reservation_vehicle_guard();
                ALTER TABLE reservations DROP CONSTRAINT reservations_no_overlap;
                ALTER TABLE reservations ADD CONSTRAINT reservations_no_overlap EXCLUDE USING gist
                (vehicle_id WITH =, tstzrange(pickup_datetime, return_datetime, '[)') WITH &&)
                WHERE (status IN ('Hold', 'UnpaidRequest', 'PendingPayment', 'Paid', 'Confirmed', 'Active'));
                """);
            migrationBuilder.DropColumn(
                name: "rental_terms",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "occupied_until_utc",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "operating_policy",
                table: "offices");

            migrationBuilder.AlterColumn<Guid>(
                name: "group_id",
                table: "vehicles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
