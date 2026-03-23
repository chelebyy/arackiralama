using RentACar.API.Contracts.FeatureFlags;

namespace RentACar.API.Services;

public interface IFeatureFlagService
{
    Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FeatureFlagDto?> UpdateAsync(string name, bool enabled, CancellationToken cancellationToken = default);
}
