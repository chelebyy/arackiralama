using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.Fleet;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces;

namespace RentACar.API.Controllers;

[Route("api/v1/vehicles")]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
public sealed class VehiclesController(
    IFleetService fleetService,
    IApplicationDbContext dbContext,
    VehicleBookingService? vehicleBookingService = null) : BaseApiController
{
    [HttpGet("available-exact")]
    public async Task<IActionResult> GetAvailableExact(
        [FromQuery] Guid pickupOfficeId, [FromQuery] Guid returnOfficeId,
        [FromQuery] DateTime pickupDateTimeUtc, [FromQuery] DateTime returnDateTimeUtc,
        [FromQuery] int? driverAge, CancellationToken cancellationToken)
    {
        if (vehicleBookingService is null) return StatusCode(503);
        pickupDateTimeUtc = pickupDateTimeUtc.Kind == DateTimeKind.Local ? pickupDateTimeUtc.ToUniversalTime() : DateTime.SpecifyKind(pickupDateTimeUtc, DateTimeKind.Utc);
        returnDateTimeUtc = returnDateTimeUtc.Kind == DateTimeKind.Local ? returnDateTimeUtc.ToUniversalTime() : DateTime.SpecifyKind(returnDateTimeUtc, DateTimeKind.Utc);
        if (pickupOfficeId == Guid.Empty || pickupDateTimeUtc <= DateTime.UtcNow ||
            pickupDateTimeUtc >= returnDateTimeUtc || returnDateTimeUtc - pickupDateTimeUtc > TimeSpan.FromDays(366))
            return BadRequestResponse("Invalid office or rental interval.");
        var offers = await vehicleBookingService.GetAvailableAsync(pickupOfficeId, returnOfficeId,
            pickupDateTimeUtc, returnDateTimeUtc, driverAge, cancellationToken);
        var results = offers.Select(offer => new
        {
            Vehicle = MapToPublicVehicle(FleetService.MapToDto(offer.Vehicle),
                offer.Vehicle.Group is { } group ? FleetService.MapToDto(group) : null, offer.Pricing.DailyRate),
            offer.Pricing.RentalDays, offer.Pricing.FinalTotal, offer.Pricing.Currency
        }).ToArray();
        return OkResponse(results);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var vehicles = await fleetService.GetVehiclesAsync(cancellationToken);
        var groups = await fleetService.GetVehicleGroupsAsync(cancellationToken);

        var publicVehicles = vehicles
            .Where(vehicle => vehicle.Status == VehicleStatus.Available)
            .Select(vehicle => MapToPublicVehicle(
                vehicle,
                groups.FirstOrDefault(group => group.Id == vehicle.GroupId),
                null))
            .OrderBy(vehicle => vehicle.Brand)
            .ThenBy(vehicle => vehicle.Model)
            .ThenBy(vehicle => vehicle.Id)
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

        if (pickupDateTimeUtc < DateTime.UtcNow || pickupDateTimeUtc >= returnDateTimeUtc)
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

        if (vehicle.Status != VehicleStatus.Available)
        {
            return NotFoundResponse("Arac bulunamadi.");
        }

        var group = vehicle.GroupId.HasValue
            ? await fleetService.GetVehicleGroupByIdAsync(vehicle.GroupId.Value, cancellationToken)
            : null;
        return OkResponse(MapToPublicVehicle(vehicle, group, null));
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

    private static PublicVehicleDto MapToPublicVehicle(VehicleDto vehicle, VehicleGroupDto? group, decimal? dailyPrice)
    {
        return new PublicVehicleDto(
            vehicle.Id,
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
            vehicle.RentalTerms?.DepositAmount ?? group?.DepositAmount ?? 0m,
            vehicle.RentalTerms?.MinAge ?? group?.MinAge ?? 0,
            vehicle.RentalTerms?.MinLicenseYears ?? group?.MinLicenseYears ?? 0,
            vehicle.Equipment ?? [],
            vehicle.Transmission,
            vehicle.FuelType,
            vehicle.SeatCount,
            vehicle.LuggageCapacity,
            vehicle.BodyType,
            vehicle.DoorCount,
            vehicle.Engine,
            vehicle.PowerHp,
            vehicle.PhotoUrls ?? (vehicle.PhotoUrl is null ? [] : [vehicle.PhotoUrl]));
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
