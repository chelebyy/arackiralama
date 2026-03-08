using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.Pricing;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class PricingService(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork,
    IHttpContextAccessor? httpContextAccessor = null) : IPricingService
{
    private const decimal AirportFeeAmount = 250m;
    private const decimal OneWayFeeAmount = 500m;
    private const decimal ExtraDriverFeeAmount = 150m;
    private const decimal ChildSeatDailyFeeAmount = 75m;
    private const decimal YoungDriverFeeAmount = 200m;
    private const decimal FullCoverageWaiverDailyFeeAmount = 350m;
    private const int YoungDriverAgeThreshold = 25;
    private const string DefaultCurrency = "TRY";
    private static readonly string[] AllowedCampaignDiscountTypes = ["percentage", "fixed"];
    private static readonly string[] AllowedPricingCalculationTypes = ["multiplier", "fixed"];

    public Task<bool> VehicleGroupExistsAsync(Guid vehicleGroupId, CancellationToken cancellationToken = default)
    {
        return dbContext.VehicleGroups
            .AsNoTracking()
            .AnyAsync(group => group.Id == vehicleGroupId, cancellationToken);
    }

    public async Task<bool> VehicleGroupsExistAsync(IReadOnlyCollection<Guid> vehicleGroupIds, CancellationToken cancellationToken = default)
    {
        if (vehicleGroupIds.Count == 0)
        {
            return true;
        }

        var distinctIds = vehicleGroupIds.Distinct().ToArray();
        var existingCount = await dbContext.VehicleGroups
            .AsNoTracking()
            .CountAsync(group => distinctIds.Contains(group.Id), cancellationToken);

        return existingCount == distinctIds.Length;
    }

    public Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default)
    {
        return dbContext.Offices
            .AsNoTracking()
            .AnyAsync(office => office.Id == officeId, cancellationToken);
    }

    public Task<bool> IsCampaignCodeAvailableAsync(string campaignCode, Guid? excludeCampaignId = null, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCampaignCode(campaignCode);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return Task.FromResult(false);
        }

        return dbContext.Campaigns
            .AsNoTracking()
            .Where(campaign => !excludeCampaignId.HasValue || campaign.Id != excludeCampaignId.Value)
            .AllAsync(campaign => campaign.Code.ToUpper() != normalizedCode, cancellationToken);
    }

    public async Task<bool> IsCampaignCodeValidAsync(
        string campaignCode,
        Guid vehicleGroupId,
        int rentalDays,
        DateOnly pickupDate,
        CancellationToken cancellationToken = default)
    {
        return await GetCampaignAsync(campaignCode, vehicleGroupId, rentalDays, pickupDate, cancellationToken) is not null;
    }

    public Task<bool> HasConflictingPricingRuleAsync(
        Guid vehicleGroupId,
        DateOnly startDate,
        DateOnly endDate,
        int priority,
        Guid? excludePricingRuleId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PricingRules
            .AsNoTracking()
            .Where(rule =>
                rule.VehicleGroupId == vehicleGroupId &&
                rule.Priority == priority &&
                (!excludePricingRuleId.HasValue || rule.Id != excludePricingRuleId.Value))
            .AnyAsync(rule => rule.StartDate <= endDate && startDate <= rule.EndDate, cancellationToken);
    }

    public async Task<PriceBreakdownDto?> CalculateBreakdownAsync(
        Guid vehicleGroupId,
        Guid pickupOfficeId,
        Guid returnOfficeId,
        DateTime pickupDateTimeUtc,
        DateTime returnDateTimeUtc,
        string? campaignCode,
        int extraDriverCount,
        int childSeatCount,
        int? driverAge,
        bool fullCoverageWaiver,
        CancellationToken cancellationToken = default)
    {
        var rentalDays = CalculateRentalDays(pickupDateTimeUtc, returnDateTimeUtc);
        var pickupDate = DateOnly.FromDateTime(pickupDateTimeUtc);

        var vehicleGroup = await dbContext.VehicleGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(group => group.Id == vehicleGroupId, cancellationToken);

        if (vehicleGroup is null)
        {
            return null;
        }

        var pricingRule = await ResolvePricingRuleAsync(vehicleGroupId, pickupDate, cancellationToken);
        if (pricingRule is null)
        {
            return null;
        }

        var officeIds = new[] { pickupOfficeId, returnOfficeId };
        var officeAirportFlags = await dbContext.Offices
            .AsNoTracking()
            .Where(office => officeIds.Contains(office.Id))
            .ToDictionaryAsync(office => office.Id, office => office.IsAirport, cancellationToken);

        var dailyRate = CalculateDailyRate(pricingRule, pickupDate);
        var baseTotal = CalculateBaseTotal(pricingRule, pickupDateTimeUtc, rentalDays);
        var airportFee = officeAirportFlags.Any(flag => flag.Value) ? AirportFeeAmount : 0m;
        var oneWayFee = pickupOfficeId != returnOfficeId ? OneWayFeeAmount : 0m;
        var extraDriverFee = extraDriverCount > 0 ? RoundAmount(extraDriverCount * ExtraDriverFeeAmount) : 0m;
        var childSeatFee = childSeatCount > 0 ? RoundAmount(childSeatCount * ChildSeatDailyFeeAmount * rentalDays) : 0m;
        var youngDriverFee = driverAge.HasValue && driverAge.Value < YoungDriverAgeThreshold ? YoungDriverFeeAmount : 0m;
        var fullCoverageWaiverFee = fullCoverageWaiver ? RoundAmount(FullCoverageWaiverDailyFeeAmount * rentalDays) : 0m;

        var extrasTotal = RoundAmount(
            airportFee +
            oneWayFee +
            extraDriverFee +
            childSeatFee +
            youngDriverFee +
            fullCoverageWaiverFee);

        var subtotalBeforeDiscount = RoundAmount(baseTotal + extrasTotal);
        var campaign = await GetCampaignAsync(campaignCode, vehicleGroupId, rentalDays, pickupDate, cancellationToken);
        var campaignDiscount = campaign is null
            ? 0m
            : CalculateCampaignDiscount(campaign, subtotalBeforeDiscount);

        campaignDiscount = Math.Clamp(campaignDiscount, 0m, subtotalBeforeDiscount);

        var depositAmount = fullCoverageWaiver ? 0m : RoundAmount(vehicleGroup.DepositAmount);
        var preAuthorizationAmount = depositAmount;
        var finalTotal = RoundAmount(subtotalBeforeDiscount - campaignDiscount);

        return new PriceBreakdownDto(
            DailyRate: dailyRate,
            RentalDays: rentalDays,
            BaseTotal: baseTotal,
            ExtrasTotal: extrasTotal,
            CampaignDiscount: campaignDiscount,
            AirportFee: airportFee,
            OneWayFee: oneWayFee,
            ExtraDriverFee: extraDriverFee,
            ChildSeatFee: childSeatFee,
            YoungDriverFee: youngDriverFee,
            FullCoverageWaiverFee: fullCoverageWaiverFee,
            FinalTotal: finalTotal,
            DepositAmount: depositAmount,
            PreAuthorizationAmount: preAuthorizationAmount,
            Currency: DefaultCurrency,
            AppliedCampaignCode: campaign?.Code);
    }

    public async Task<IReadOnlyList<PricingRuleDto>> GetPricingRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.PricingRules
            .AsNoTracking()
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.StartDate)
            .ThenBy(rule => rule.VehicleGroupId)
            .ToListAsync(cancellationToken);

        return rules.Select(MapToDto).ToList();
    }

    public async Task<PricingRuleDto> CreatePricingRuleAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default)
    {
        var pricingRule = new PricingRule
        {
            VehicleGroupId = request.VehicleGroupId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DailyPrice = request.DailyPrice,
            Multiplier = request.Multiplier,
            WeekdayMultiplier = request.WeekdayMultiplier,
            WeekendMultiplier = request.WeekendMultiplier,
            CalculationType = NormalizePricingCalculationType(request.CalculationType),
            Priority = request.Priority
        };

        dbContext.PricingRules.Add(pricingRule);
        WriteAuditLog(
            action: "PricingRuleCreated",
            entityType: nameof(PricingRule),
            entityId: pricingRule.Id,
            details: new
            {
                pricingRule.VehicleGroupId,
                pricingRule.StartDate,
                pricingRule.EndDate,
                pricingRule.DailyPrice,
                pricingRule.Multiplier,
                pricingRule.WeekdayMultiplier,
                pricingRule.WeekendMultiplier,
                pricingRule.CalculationType,
                pricingRule.Priority
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(pricingRule);
    }

    public async Task<PricingRuleDto?> UpdatePricingRuleAsync(Guid id, UpdatePricingRuleRequest request, CancellationToken cancellationToken = default)
    {
        var pricingRule = await dbContext.PricingRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);
        if (pricingRule is null)
        {
            return null;
        }

        var previousState = new
        {
            pricingRule.VehicleGroupId,
            pricingRule.StartDate,
            pricingRule.EndDate,
            pricingRule.DailyPrice,
            pricingRule.Multiplier,
            pricingRule.WeekdayMultiplier,
            pricingRule.WeekendMultiplier,
            pricingRule.CalculationType,
            pricingRule.Priority
        };

        pricingRule.VehicleGroupId = request.VehicleGroupId;
        pricingRule.StartDate = request.StartDate;
        pricingRule.EndDate = request.EndDate;
        pricingRule.DailyPrice = request.DailyPrice;
        pricingRule.Multiplier = request.Multiplier;
        pricingRule.WeekdayMultiplier = request.WeekdayMultiplier;
        pricingRule.WeekendMultiplier = request.WeekendMultiplier;
        pricingRule.CalculationType = NormalizePricingCalculationType(request.CalculationType);
        pricingRule.Priority = request.Priority;

        WriteAuditLog(
            action: "PricingRuleUpdated",
            entityType: nameof(PricingRule),
            entityId: pricingRule.Id,
            details: new
            {
                Previous = previousState,
                Current = new
                {
                    pricingRule.VehicleGroupId,
                    pricingRule.StartDate,
                    pricingRule.EndDate,
                    pricingRule.DailyPrice,
                    pricingRule.Multiplier,
                    pricingRule.WeekdayMultiplier,
                    pricingRule.WeekendMultiplier,
                    pricingRule.CalculationType,
                    pricingRule.Priority
                }
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(pricingRule);
    }

    public async Task<bool> DeletePricingRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pricingRule = await dbContext.PricingRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);
        if (pricingRule is null)
        {
            return false;
        }

        WriteAuditLog(
            action: "PricingRuleDeleted",
            entityType: nameof(PricingRule),
            entityId: pricingRule.Id,
            details: new
            {
                pricingRule.VehicleGroupId,
            pricingRule.StartDate,
            pricingRule.EndDate,
            pricingRule.WeekdayMultiplier,
            pricingRule.WeekendMultiplier,
            pricingRule.Priority
        });
        dbContext.PricingRules.Remove(pricingRule);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CampaignDto>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await dbContext.Campaigns
            .AsNoTracking()
            .OrderBy(campaign => campaign.Code)
            .ToListAsync(cancellationToken);

        return campaigns.Select(MapToDto).ToList();
    }

    public async Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = new Campaign
        {
            Code = NormalizeCampaignCode(request.Code),
            DiscountType = NormalizeCampaignDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue,
            MinDays = request.MinDays,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            IsActive = request.IsActive,
            AllowedVehicleGroupIds = NormalizeVehicleGroupIds(request.AllowedVehicleGroupIds)
        };

        dbContext.Campaigns.Add(campaign);
        WriteAuditLog(
            action: "CampaignCreated",
            entityType: nameof(Campaign),
            entityId: campaign.Id,
            details: new
            {
                campaign.Code,
                campaign.DiscountType,
                campaign.DiscountValue,
                campaign.MinDays,
                campaign.ValidFrom,
                campaign.ValidUntil,
                campaign.IsActive,
                campaign.AllowedVehicleGroupIds
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(campaign);
    }

    public async Task<CampaignDto?> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        var previousState = new
        {
            campaign.Code,
            campaign.DiscountType,
            campaign.DiscountValue,
            campaign.MinDays,
            campaign.ValidFrom,
            campaign.ValidUntil,
            campaign.IsActive,
            AllowedVehicleGroupIds = campaign.AllowedVehicleGroupIds.ToList()
        };

        campaign.Code = NormalizeCampaignCode(request.Code);
        campaign.DiscountType = NormalizeCampaignDiscountType(request.DiscountType);
        campaign.DiscountValue = request.DiscountValue;
        campaign.MinDays = request.MinDays;
        campaign.ValidFrom = request.ValidFrom;
        campaign.ValidUntil = request.ValidUntil;
        campaign.IsActive = request.IsActive;
        campaign.AllowedVehicleGroupIds = NormalizeVehicleGroupIds(request.AllowedVehicleGroupIds);

        WriteAuditLog(
            action: "CampaignUpdated",
            entityType: nameof(Campaign),
            entityId: campaign.Id,
            details: new
            {
                Previous = previousState,
                Current = new
                {
                    campaign.Code,
                    campaign.DiscountType,
                    campaign.DiscountValue,
                    campaign.MinDays,
                    campaign.ValidFrom,
                    campaign.ValidUntil,
                    campaign.IsActive,
                    campaign.AllowedVehicleGroupIds
                }
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(campaign);
    }

    public async Task<bool> DeleteCampaignAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (campaign is null)
        {
            return false;
        }

        WriteAuditLog(
            action: "CampaignDeleted",
            entityType: nameof(Campaign),
            entityId: campaign.Id,
            details: new
            {
                campaign.Code,
                campaign.ValidFrom,
                campaign.ValidUntil
            });
        dbContext.Campaigns.Remove(campaign);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PricingRule?> ResolvePricingRuleAsync(Guid vehicleGroupId, DateOnly pickupDate, CancellationToken cancellationToken)
    {
        return await dbContext.PricingRules
            .AsNoTracking()
            .Where(rule =>
                rule.VehicleGroupId == vehicleGroupId &&
                rule.StartDate <= pickupDate &&
                rule.EndDate >= pickupDate)
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => rule.StartDate)
            .ThenByDescending(rule => rule.EndDate)
            .ThenByDescending(rule => rule.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Campaign?> GetCampaignAsync(
        string? campaignCode,
        Guid vehicleGroupId,
        int rentalDays,
        DateOnly pickupDate,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCampaignCode(campaignCode);
        if (string.IsNullOrEmpty(normalizedCode))
        {
            return null;
        }

        var campaignCandidates = await dbContext.Campaigns
            .AsNoTracking()
            .Where(campaign =>
                campaign.Code.ToUpper() == normalizedCode &&
                campaign.IsActive &&
                campaign.ValidFrom <= pickupDate &&
                campaign.ValidUntil >= pickupDate &&
                campaign.MinDays <= rentalDays)
            .OrderByDescending(campaign => campaign.ValidFrom)
            .ThenByDescending(campaign => campaign.CreatedAt)
            .ToListAsync(cancellationToken);

        return campaignCandidates.FirstOrDefault(campaign =>
            campaign.AllowedVehicleGroupIds.Count == 0 ||
            campaign.AllowedVehicleGroupIds.Contains(vehicleGroupId));
    }

    private void WriteAuditLog(string action, string entityType, Guid entityId, object details)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            UserId = GetCurrentActorId(),
            Timestamp = DateTime.UtcNow,
            Details = JsonSerializer.Serialize(details)
        });
    }

    private string? GetCurrentActorId()
    {
        var user = httpContextAccessor?.HttpContext?.User;
        return user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.Identity?.Name;
    }

    private static PricingRuleDto MapToDto(PricingRule pricingRule)
    {
        return new PricingRuleDto(
            pricingRule.Id,
            pricingRule.VehicleGroupId,
            pricingRule.StartDate,
            pricingRule.EndDate,
            pricingRule.DailyPrice,
            pricingRule.Multiplier,
            pricingRule.WeekdayMultiplier,
            pricingRule.WeekendMultiplier,
            pricingRule.CalculationType,
            pricingRule.Priority);
    }

    private static CampaignDto MapToDto(Campaign campaign)
    {
        return new CampaignDto(
            campaign.Id,
            campaign.Code,
            campaign.DiscountType,
            campaign.DiscountValue,
            campaign.MinDays,
            campaign.ValidFrom,
            campaign.ValidUntil,
            campaign.IsActive,
            campaign.AllowedVehicleGroupIds);
    }

    private static decimal CalculateCampaignDiscount(Campaign campaign, decimal subtotalBeforeDiscount)
    {
        return NormalizeCampaignDiscountType(campaign.DiscountType) switch
        {
            "percentage" => RoundAmount(subtotalBeforeDiscount * campaign.DiscountValue / 100m),
            "fixed" => RoundAmount(campaign.DiscountValue),
            _ => 0m
        };
    }

    private static decimal CalculateBaseTotal(PricingRule pricingRule, DateTime pickupDateTimeUtc, int rentalDays)
    {
        var total = 0m;
        var pickupDate = DateOnly.FromDateTime(pickupDateTimeUtc);
        for (var dayOffset = 0; dayOffset < rentalDays; dayOffset++)
        {
            var currentDate = pickupDate.AddDays(dayOffset);
            var dailyRate = CalculateDailyRate(pricingRule, currentDate);
            total += dailyRate;
        }

        return RoundAmount(total);
    }

    private static decimal CalculateDailyRate(PricingRule pricingRule, DateOnly date)
    {
        var baseRate = NormalizePricingCalculationType(pricingRule.CalculationType) switch
        {
            "fixed" => pricingRule.DailyPrice,
            _ => pricingRule.DailyPrice * ResolveMultiplier(pricingRule.Multiplier)
        };

        var dayMultiplier = IsWeekend(date)
            ? ResolveMultiplier(pricingRule.WeekendMultiplier)
            : ResolveMultiplier(pricingRule.WeekdayMultiplier);

        return RoundAmount(baseRate * dayMultiplier);
    }

    private static string NormalizeCampaignCode(string? campaignCode)
    {
        return campaignCode?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string NormalizeCampaignDiscountType(string discountType)
    {
        var normalized = discountType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "percent" or "pct" => "percentage",
            "amount" => "fixed",
            _ => normalized
        };
    }

    private static string NormalizePricingCalculationType(string calculationType)
    {
        return calculationType.Trim().ToLowerInvariant();
    }

    private static List<Guid> NormalizeVehicleGroupIds(IReadOnlyList<Guid>? vehicleGroupIds)
    {
        return (vehicleGroupIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    public static bool IsSupportedCampaignDiscountType(string discountType)
    {
        return AllowedCampaignDiscountTypes.Contains(NormalizeCampaignDiscountType(discountType), StringComparer.Ordinal);
    }

    public static bool IsSupportedPricingCalculationType(string calculationType)
    {
        return AllowedPricingCalculationTypes.Contains(NormalizePricingCalculationType(calculationType), StringComparer.Ordinal);
    }

    private static decimal ResolveMultiplier(decimal multiplier)
    {
        return multiplier <= 0m ? 1m : multiplier;
    }

    private static bool IsWeekend(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static decimal RoundAmount(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static int CalculateRentalDays(DateTime pickupDateTimeUtc, DateTime returnDateTimeUtc)
    {
        var pickupDate = DateOnly.FromDateTime(pickupDateTimeUtc);
        var returnDate = DateOnly.FromDateTime(returnDateTimeUtc);
        return Math.Max(1, returnDate.DayNumber - pickupDate.DayNumber);
    }
}
