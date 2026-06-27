using RentACar.API.Contracts.PublicSiteSettings;

namespace RentACar.API.Services;

public interface IPublicSiteSettingsService
{
    Task<PublicSiteSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<PublicSiteSettingsDto> UpdateAsync(UpdatePublicSiteSettingsRequest request, CancellationToken cancellationToken = default);
    Task<AdminPublicContentDto> GetAdminContentAsync(CancellationToken cancellationToken = default);
    Task<AdminPublicContentDto> UpdatePageDraftAsync(string slug, string locale, UpdateAdminPublicPageDraftRequest request, CancellationToken cancellationToken = default);
    Task<AdminPublicContentDto> PublishPageAsync(string slug, string locale, PublishAdminPublicPageRequest request, CancellationToken cancellationToken = default);
    Task<AdminPublicContentDto> UnpublishPageAsync(string slug, string locale, PublishAdminPublicPageRequest request, CancellationToken cancellationToken = default);
    Task<AdminPublicContentDto> UpdateContactContentAsync(UpdateAdminPublicContactRequest request, CancellationToken cancellationToken = default);
}
