using RentACar.API.Contracts.Pricing;

namespace RentACar.API.Services;

public interface IPricingService
{
    Task<bool> VehicleGroupExistsAsync(Guid vehicleGroupId, CancellationToken cancellationToken = default);
    Task<bool> VehicleGroupsExistAsync(IReadOnlyCollection<Guid> vehicleGroupIds, CancellationToken cancellationToken = default);
    Task<bool> OfficeExistsAsync(Guid officeId, CancellationToken cancellationToken = default);
    Task<bool> IsCampaignCodeAvailableAsync(string campaignCode, Guid? excludeCampaignId = null, CancellationToken cancellationToken = default);
    Task<bool> IsCampaignCodeValidAsync(string campaignCode, Guid vehicleGroupId, int rentalDays, DateOnly pickupDate, CancellationToken cancellationToken = default);
    Task<bool> HasConflictingPricingRuleAsync(Guid vehicleGroupId, DateOnly startDate, DateOnly endDate, int priority, Guid? excludePricingRuleId = null, CancellationToken cancellationToken = default);
    Task<PriceBreakdownDto?> CalculateBreakdownAsync(
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
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PricingRuleDto>> GetPricingRulesAsync(CancellationToken cancellationToken = default);
    Task<PricingRuleDto> CreatePricingRuleAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default);
    Task<PricingRuleDto?> UpdatePricingRuleAsync(Guid id, UpdatePricingRuleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePricingRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignDto>> GetCampaignsAsync(CancellationToken cancellationToken = default);
    Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default);
    Task<CampaignDto?> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteCampaignAsync(Guid id, CancellationToken cancellationToken = default);
}
