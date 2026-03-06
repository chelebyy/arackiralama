namespace RentACar.Infrastructure.Data.Configurations;

internal static class SeedDataConstants
{
    internal static readonly Guid AlanyaCenterOfficeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid GazipasaAirportOfficeId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    internal static readonly Guid EconomyGroupId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    internal static readonly Guid SuvGroupId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid OnlinePaymentFeatureFlagId = Guid.Parse("33333333-3333-3333-3333-333333333331");
    internal static readonly Guid CampaignsFeatureFlagId = Guid.Parse("33333333-3333-3333-3333-333333333332");
    internal static readonly DateTime SeededAtUtc = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
}
