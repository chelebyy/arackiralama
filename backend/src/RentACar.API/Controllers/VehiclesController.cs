using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/v1/vehicles")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class VehiclesController(
    IFleetService fleetService,
    IApplicationDbContext dbContext) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var vehicles = await fleetService.GetVehiclesAsync(cancellationToken);
        var groups = await fleetService.GetVehicleGroupsAsync(cancellationToken);
        var dailyPrices = await ResolveDailyPricesAsync(
            vehicles.Select(vehicle => vehicle.GroupId).Distinct().ToArray(),
            cancellationToken);

        var publicVehicles = vehicles
            .Select(vehicle => MapToPublicVehicle(
                vehicle,
                groups.FirstOrDefault(group => group.Id == vehicle.GroupId),
                dailyPrices.GetValueOrDefault(vehicle.GroupId)))
            .OrderBy(vehicle => vehicle.Brand)
            .ThenBy(vehicle => vehicle.Model)
            .ThenBy(vehicle => vehicle.Plate)
            .ToList();

        return OkResponse(publicVehicles);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        var groups = await fleetService.GetVehicleGroupsAsync(cancellationToken);
        return OkResponse(groups);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(
        [FromQuery(Name = "office_id")] Guid officeId,
        [FromQuery(Name = "pickup_datetime")] DateTime pickupDateTimeUtc,
        [FromQuery(Name = "return_datetime")] DateTime returnDateTimeUtc,
        [FromQuery(Name = "vehicle_group_id")] Guid? vehicleGroupId,
        CancellationToken cancellationToken)
    {
        if (pickupDateTimeUtc.Kind == DateTimeKind.Unspecified)
        {
            pickupDateTimeUtc = DateTime.SpecifyKind(pickupDateTimeUtc, DateTimeKind.Utc);
        }

        if (returnDateTimeUtc.Kind == DateTimeKind.Unspecified)
        {
            returnDateTimeUtc = DateTime.SpecifyKind(returnDateTimeUtc, DateTimeKind.Utc);
        }

        if (officeId == Guid.Empty)
        {
            return BadRequestResponse("Gecerli bir ofis secilmelidir.");
        }

        if (pickupDateTimeUtc >= returnDateTimeUtc)
        {
            return BadRequestResponse("Alis tarihi donus tarihinden once olmalidir.");
        }

        if (!await fleetService.OfficeExistsAsync(officeId, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir ofis secilmelidir.");
        }

        if (vehicleGroupId.HasValue && !await fleetService.VehicleGroupExistsAsync(vehicleGroupId.Value, cancellationToken))
        {
            return BadRequestResponse("Gecerli bir arac grubu secilmelidir.");
        }

        var availableGroups = await fleetService.SearchAvailableVehicleGroupsAsync(
            officeId,
            pickupDateTimeUtc,
            returnDateTimeUtc,
            vehicleGroupId,
            cancellationToken);

        return OkResponse(availableGroups);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await fleetService.GetVehicleByIdAsync(id, cancellationToken);
        if (vehicle is null)
        {
            return NotFoundResponse("Arac bulunamadi.");
        }

        var group = await fleetService.GetVehicleGroupByIdAsync(vehicle.GroupId, cancellationToken);
        var dailyPrices = await ResolveDailyPricesAsync([vehicle.GroupId], cancellationToken);
        return OkResponse(MapToPublicVehicle(vehicle, group, dailyPrices.GetValueOrDefault(vehicle.GroupId)));
    }

    private async Task<Dictionary<Guid, decimal>> ResolveDailyPricesAsync(
        IReadOnlyCollection<Guid> vehicleGroupIds,
        CancellationToken cancellationToken)
    {
        if (vehicleGroupIds.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var candidateRules = await dbContext.PricingRules
            .AsNoTracking()
            .Where(rule =>
                vehicleGroupIds.Contains(rule.VehicleGroupId) &&
                rule.StartDate <= today &&
                rule.EndDate >= today)
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => rule.StartDate)
            .ThenByDescending(rule => rule.EndDate)
            .ThenByDescending(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken);

        return candidateRules
            .GroupBy(rule => rule.VehicleGroupId)
            .ToDictionary(grouping => grouping.Key, grouping => CalculateDailyRate(grouping.First(), today));
    }

    private static PublicVehicleDto MapToPublicVehicle(VehicleDto vehicle, VehicleGroupDto? group, decimal dailyPrice)
    {
        return new PublicVehicleDto(
            vehicle.Id,
            vehicle.Plate,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Year,
            vehicle.Color,
            vehicle.GroupId,
            group?.NameTr ?? string.Empty,
            group?.NameEn ?? string.Empty,
            vehicle.OfficeId,
            vehicle.Status.ToString(),
            vehicle.PhotoUrl,
            dailyPrice,
            group?.DepositAmount ?? 0m,
            group?.MinAge ?? 0,
            group?.MinLicenseYears ?? 0,
            group?.Features ?? []);
    }

    private static decimal CalculateDailyRate(PricingRule pricingRule, DateOnly date)
    {
        var calculationType = pricingRule.CalculationType.Trim().ToLowerInvariant();
        var baseRate = calculationType == "fixed"
            ? pricingRule.DailyPrice
            : pricingRule.DailyPrice * ResolveMultiplier(pricingRule.Multiplier);
        var dayMultiplier = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? ResolveMultiplier(pricingRule.WeekendMultiplier)
            : ResolveMultiplier(pricingRule.WeekdayMultiplier);

        return decimal.Round(baseRate * dayMultiplier, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveMultiplier(decimal multiplier)
    {
        return multiplier <= 0m ? 1m : multiplier;
    }
}
