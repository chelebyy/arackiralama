using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentACar.Infrastructure.Data.Configurations;

namespace RentACar.Infrastructure.Data;

public static class ReservationExtraOptionsBackfillExtensions
{
    private static readonly Guid[] BuiltInOptionIds =
    [
        SeedDataConstants.ChildSeatExtraOptionId,
        SeedDataConstants.AdditionalDriverExtraOptionId,
        SeedDataConstants.GpsExtraOptionId,
        SeedDataConstants.WifiExtraOptionId
    ];

    public static async Task ApplyReservationExtraOptionsBackfillAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var dbContext = serviceProvider.GetRequiredService<RentACarDbContext>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ReservationExtraOptionsBackfill");

        var hasAnyBuiltInAssignment = await dbContext.ReservationExtraOptionVehicleGroups
            .AnyAsync(assignment => BuiltInOptionIds.Contains(assignment.OptionId), cancellationToken);
        if (hasAnyBuiltInAssignment)
        {
            return;
        }

        var affectedRows = await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO reservation_extra_option_vehicle_groups (option_id, vehicle_group_id)
            SELECT option_ids.option_id, vehicle_groups.id
            FROM (VALUES
                ('44444444-4444-4444-4444-444444444441'::uuid),
                ('44444444-4444-4444-4444-444444444442'::uuid),
                ('44444444-4444-4444-4444-444444444443'::uuid),
                ('44444444-4444-4444-4444-444444444444'::uuid)
            ) AS option_ids(option_id)
            INNER JOIN reservation_extra_options option
                ON option.id = option_ids.option_id
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
              )
              AND (
                  SELECT COUNT(DISTINCT translation.locale)
                  FROM reservation_extra_option_translations translation
                  WHERE translation.option_id = option.id
                    AND translation.locale IN ('tr', 'en', 'de', 'ru', 'ar')
              ) = 5;
            """,
            cancellationToken);

        if (affectedRows > 0)
        {
            logger.LogInformation("Reservation extra option assignment backfill affected {AffectedRows} rows.", affectedRows);
        }
    }
}
