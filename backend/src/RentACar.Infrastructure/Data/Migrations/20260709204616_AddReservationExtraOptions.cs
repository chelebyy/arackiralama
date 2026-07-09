using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RentACar.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationExtraOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pricing_snapshot",
                table: "reservations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "quote_id",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reservation_extra_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pricing_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_quantity = table.Column<int>(type: "integer", nullable: false),
                    icon_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_extra_options", x => x.id);
                    table.CheckConstraint("ck_reservation_extra_options_icon_key", "icon_key IN ('baby', 'users', 'navigation', 'wifi')");
                    table.CheckConstraint("ck_reservation_extra_options_max_quantity", "max_quantity >= 1 AND max_quantity <= 20");
                    table.CheckConstraint("ck_reservation_extra_options_sort_order", "sort_order >= 0 AND sort_order <= 9999");
                    table.CheckConstraint("ck_reservation_extra_options_unit_price", "unit_price >= 0 AND unit_price <= 1000000");
                });

            migrationBuilder.CreateTable(
                name: "reservation_extra_option_translations",
                columns: table => new
                {
                    option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_extra_option_translations", x => new { x.option_id, x.locale });
                    table.CheckConstraint("ck_reservation_extra_option_translations_locale", "locale IN ('tr', 'en', 'de', 'ru', 'ar')");
                    table.ForeignKey(
                        name: "FK_reservation_extra_option_translations_reservation_extra_opt~",
                        column: x => x.option_id,
                        principalTable: "reservation_extra_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reservation_extra_option_vehicle_groups",
                columns: table => new
                {
                    option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_extra_option_vehicle_groups", x => new { x.option_id, x.vehicle_group_id });
                    table.ForeignKey(
                        name: "FK_reservation_extra_option_vehicle_groups_reservation_extra_o~",
                        column: x => x.option_id,
                        principalTable: "reservation_extra_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reservation_extra_option_vehicle_groups_vehicle_groups_vehi~",
                        column: x => x.vehicle_group_id,
                        principalTable: "vehicle_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reservation_selected_extras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extra_option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_version_snapshot = table.Column<long>(type: "bigint", nullable: false),
                    locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    option_code_snapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_snapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pricing_mode_snapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    rental_days_snapshot = table.Column<int>(type: "integer", nullable: false),
                    total_price_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_selected_extras", x => x.id);
                    table.CheckConstraint("ck_reservation_selected_extras_currency", "currency = 'TRY'");
                    table.CheckConstraint("ck_reservation_selected_extras_prices", "unit_price_snapshot >= 0 AND total_price_snapshot >= 0");
                    table.CheckConstraint("ck_reservation_selected_extras_quantity", "quantity >= 1 AND quantity <= 20");
                    table.ForeignKey(
                        name: "FK_reservation_selected_extras_reservation_extra_options_extra~",
                        column: x => x.extra_option_id,
                        principalTable: "reservation_extra_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservation_selected_extras_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "reservation_extra_options",
                columns: new[] { "id", "code", "created_at", "icon_key", "is_active", "is_archived", "max_quantity", "pricing_mode", "sort_order", "unit_price", "updated_at" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444441"), "child_seat", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "baby", false, false, 20, "PER_DAY", 10, 75m, new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444442"), "additional_driver", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "users", false, false, 20, "PER_RENTAL", 20, 150m, new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444443"), "gps", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "navigation", false, false, 1, "PER_DAY", 30, 8m, new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "wifi", new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), "wifi", false, false, 1, "PER_DAY", 40, 12m, new DateTime(2026, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "reservation_extra_option_translations",
                columns: new[] { "locale", "option_id", "description", "name" },
                values: new object[,]
                {
                    { "ar", new Guid("44444444-4444-4444-4444-444444444441"), "مناسب للأطفال 9-36 كجم", "مقعد أطفال" },
                    { "de", new Guid("44444444-4444-4444-4444-444444444441"), "Geeignet für Kinder 9-36 kg", "Kindersitz" },
                    { "en", new Guid("44444444-4444-4444-4444-444444444441"), "Suitable for children 9-36 kg", "Child Seat" },
                    { "ru", new Guid("44444444-4444-4444-4444-444444444441"), "Подходит для детей 9-36 кг", "Детское кресло" },
                    { "tr", new Guid("44444444-4444-4444-4444-444444444441"), "9-36 kg arası çocuklar için uygundur", "Çocuk Koltuğu" },
                    { "ar", new Guid("44444444-4444-4444-4444-444444444442"), "يمكن لسائق إضافي قيادة السيارة أيضاً", "سائق إضافي" },
                    { "de", new Guid("44444444-4444-4444-4444-444444444442"), "Ein zusätzlicher Fahrer kann das Fahrzeug auch fahren", "Zusätzlicher Fahrer" },
                    { "en", new Guid("44444444-4444-4444-4444-444444444442"), "An additional driver can also drive the vehicle", "Additional Driver" },
                    { "ru", new Guid("44444444-4444-4444-4444-444444444442"), "Дополнительный водитель также может управлять автомобилем", "Дополнительный водитель" },
                    { "tr", new Guid("44444444-4444-4444-4444-444444444442"), "Araçta ek sürücü de kullanabilir", "Ek Sürücü" },
                    { "ar", new Guid("44444444-4444-4444-4444-444444444443"), "لا تضل الطريق مع جهاز الملاحة", "جهاز ملاحة GPS" },
                    { "de", new Guid("44444444-4444-4444-4444-444444444443"), "Mit Navigationsgerät nie verloren gehen", "GPS-Navigation" },
                    { "en", new Guid("44444444-4444-4444-4444-444444444443"), "Never get lost with navigation device", "GPS Navigation" },
                    { "ru", new Guid("44444444-4444-4444-4444-444444444443"), "Никогда не теряйтесь с навигационным устройством", "GPS-навигатор" },
                    { "tr", new Guid("44444444-4444-4444-4444-444444444443"), "Navigasyon cihazıyla asla kaybolmayın", "GPS Navigasyon" },
                    { "ar", new Guid("44444444-4444-4444-4444-444444444444"), "ابقَ متصلاً بالإنترنت داخل السيارة", "واي فاي" },
                    { "de", new Guid("44444444-4444-4444-4444-444444444444"), "Bleiben Sie mit Internet im Auto verbunden", "WiFi" },
                    { "en", new Guid("44444444-4444-4444-4444-444444444444"), "Stay connected with in-car internet", "WiFi" },
                    { "ru", new Guid("44444444-4444-4444-4444-444444444444"), "Оставайтесь на связи с интернетом в автомобиле", "WiFi" },
                    { "tr", new Guid("44444444-4444-4444-4444-444444444444"), "Araç içinde internetle bağlı kalın", "WiFi" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_quote_id",
                table: "reservations",
                column: "quote_id",
                unique: true,
                filter: "quote_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_reservation_extra_option_translations_locale",
                table: "reservation_extra_option_translations",
                column: "locale");

            migrationBuilder.CreateIndex(
                name: "idx_reservation_extra_option_vehicle_groups_group",
                table: "reservation_extra_option_vehicle_groups",
                column: "vehicle_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_extra_options_code",
                table: "reservation_extra_options",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_reservation_extra_options_catalog",
                table: "reservation_extra_options",
                columns: new[] { "is_active", "is_archived", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_selected_extras_extra_option_id",
                table: "reservation_selected_extras",
                column: "extra_option_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_selected_extras_reservation_id_extra_option_id",
                table: "reservation_selected_extras",
                columns: new[] { "reservation_id", "extra_option_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_reservation_selected_extras_reservation",
                table: "reservation_selected_extras",
                column: "reservation_id");

            migrationBuilder.Sql(
                """
                INSERT INTO reservation_extra_option_vehicle_groups (option_id, vehicle_group_id)
                SELECT option_ids.option_id, vehicle_groups.id
                FROM (VALUES
                    ('44444444-4444-4444-4444-444444444441'::uuid),
                    ('44444444-4444-4444-4444-444444444442'::uuid),
                    ('44444444-4444-4444-4444-444444444443'::uuid),
                    ('44444444-4444-4444-4444-444444444444'::uuid)
                ) AS option_ids(option_id)
                CROSS JOIN vehicle_groups
                ON CONFLICT DO NOTHING;

                UPDATE reservation_extra_options option
                SET is_active = TRUE,
                    updated_at = NOW()
                WHERE option.id IN (
                    '44444444-4444-4444-4444-444444444441'::uuid,
                    '44444444-4444-4444-4444-444444444442'::uuid,
                    '44444444-4444-4444-4444-444444444443'::uuid,
                    '44444444-4444-4444-4444-444444444444'::uuid
                )
                  AND EXISTS (
                      SELECT 1
                      FROM reservation_extra_option_vehicle_groups assignment
                      WHERE assignment.option_id = option.id
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_extra_option_translations");

            migrationBuilder.DropTable(
                name: "reservation_extra_option_vehicle_groups");

            migrationBuilder.DropTable(
                name: "reservation_selected_extras");

            migrationBuilder.DropTable(
                name: "reservation_extra_options");

            migrationBuilder.DropIndex(
                name: "IX_reservations_quote_id",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "pricing_snapshot",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "quote_id",
                table: "reservations");
        }
    }
}
