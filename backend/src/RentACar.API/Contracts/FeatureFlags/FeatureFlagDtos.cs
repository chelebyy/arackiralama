namespace RentACar.API.Contracts.FeatureFlags;

public sealed record FeatureFlagDto(
    Guid Id,
    string Name,
    bool Enabled,
    string Description,
    DateTime UpdatedAtUtc);

public sealed record UpdateFeatureFlagRequest(bool Enabled);
