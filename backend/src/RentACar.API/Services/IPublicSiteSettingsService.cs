using RentACar.API.Contracts.PublicSiteSettings;

namespace RentACar.API.Services;

public interface IPublicSiteSettingsService
{
    Task<PublicSiteSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<PublicSiteSettingsDto> UpdateAsync(UpdatePublicSiteSettingsRequest request, CancellationToken cancellationToken = default);
}
