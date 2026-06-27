namespace RentACar.API.Contracts.PublicSiteSettings;

public sealed record PublicLocalizedTextDto(
    string? Label = null,
    string? Value = null,
    string? Description = null,
    string? Name = null,
    string? Address = null,
    string? Hours = null,
    string? Day = null);

public sealed record PublicSiteLinkDto(
    string Id,
    string Label,
    string Href,
    bool IsVisible,
    int SortOrder,
    IReadOnlyDictionary<string, PublicLocalizedTextDto>? Translations = null);

public sealed record PublicSocialLinkDto(
    string Id,
    string Platform,
    string Url,
    bool IsVisible,
    int SortOrder);

public sealed record PublicContactChannelDto(
    string Id,
    string Type,
    string Label,
    string Value,
    string Href,
    string Description,
    bool IsVisible,
    int SortOrder,
    IReadOnlyDictionary<string, PublicLocalizedTextDto>? Translations = null);

public sealed record PublicContactOfficeDto(
    string Id,
    string Name,
    string Address,
    string Phone,
    string Hours,
    string Type,
    bool IsVisible,
    int SortOrder,
    IReadOnlyDictionary<string, PublicLocalizedTextDto>? Translations = null);

public sealed record PublicContactWorkingHourDto(
    string Id,
    string Day,
    string Hours,
    bool IsVisible,
    int SortOrder,
    IReadOnlyDictionary<string, PublicLocalizedTextDto>? Translations = null);

public sealed record PublicPageBlockDto(
    string Id,
    string Heading,
    string Body,
    bool IsVisible,
    int SortOrder,
    string BodyFormat = "plain");

public sealed record PublicManagedPageDto(
    string Id,
    string Slug,
    string Locale,
    string Title,
    string Subtitle,
    string SeoTitle,
    string SeoDescription,
    bool IsPublished,
    int SortOrder,
    IReadOnlyList<PublicPageBlockDto> Blocks);

public sealed record PublicPagePublishedSnapshotDto(
    string Title,
    string Subtitle,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<PublicPageBlockDto> Blocks,
    DateTime PublishedAtUtc);

public sealed record AdminPublicManagedPageDto(
    string Id,
    string Slug,
    string Locale,
    string Title,
    string Subtitle,
    string SeoTitle,
    string SeoDescription,
    bool IsPublished,
    int SortOrder,
    IReadOnlyList<PublicPageBlockDto> Blocks,
    PublicPagePublishedSnapshotDto? Published,
    DateTime? DraftUpdatedAtUtc,
    DateTime? PublishedAtUtc);

public sealed record AdminPublicContentDto(
    string Version,
    DateTime UpdatedAt,
    IReadOnlyList<AdminPublicManagedPageDto> Pages,
    IReadOnlyList<PublicContactChannelDto> ContactPageChannels,
    IReadOnlyList<PublicContactOfficeDto> ContactPageOffices,
    IReadOnlyList<PublicContactWorkingHourDto> ContactPageWorkingHours,
    string ContactPageMapTitle,
    string ContactPageMapEmbedUrl,
    bool ContactPageMapIsVisible);

public sealed record UpdateAdminPublicPageDraftRequest(
    string Version,
    string Title,
    string Subtitle,
    string SeoTitle,
    string SeoDescription,
    bool IsPublished,
    int SortOrder,
    IReadOnlyList<PublicPageBlockDto> Blocks);

public sealed record PublishAdminPublicPageRequest(string Version);

public sealed record UpdateAdminPublicContactRequest(
    string Version,
    IReadOnlyList<PublicContactChannelDto> ContactPageChannels,
    IReadOnlyList<PublicContactOfficeDto> ContactPageOffices,
    IReadOnlyList<PublicContactWorkingHourDto> ContactPageWorkingHours,
    string ContactPageMapTitle,
    string ContactPageMapEmbedUrl,
    bool ContactPageMapIsVisible);

public sealed record PublicPaymentMethodsDto(
    bool CreditCardEnabled,
    bool DebitCardEnabled,
    bool UnpaidRequestEnabled,
    bool PaypalEnabled,
    bool AnyEnabled);

public sealed record PublicSiteSettingsDto(
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string CompanyEmail,
    string WorkingHours,
    IReadOnlyList<PublicSiteLinkDto> HeaderLinks,
    IReadOnlyList<PublicSiteLinkDto> HeroLinks,
    IReadOnlyList<PublicSiteLinkDto> QuickLinks,
    IReadOnlyList<PublicSocialLinkDto> SocialLinks,
    IReadOnlyList<PublicSiteLinkDto> FooterBottomLinks,
    IReadOnlyList<PublicContactChannelDto> ContactPageChannels,
    IReadOnlyList<PublicContactOfficeDto> ContactPageOffices,
    IReadOnlyList<PublicContactWorkingHourDto> ContactPageWorkingHours,
    string ContactPageMapTitle,
    string ContactPageMapEmbedUrl,
    bool ContactPageMapIsVisible,
    IReadOnlyList<PublicManagedPageDto> Pages,
    PublicPaymentMethodsDto PaymentMethods,
    bool OnlinePaymentEnabled,
    DateTime UpdatedAt);

public sealed record UpdatePublicSiteSettingsRequest(
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string CompanyEmail,
    string WorkingHours,
    IReadOnlyList<PublicSiteLinkDto> HeaderLinks,
    IReadOnlyList<PublicSiteLinkDto> HeroLinks,
    IReadOnlyList<PublicSiteLinkDto> QuickLinks,
    IReadOnlyList<PublicSocialLinkDto> SocialLinks,
    IReadOnlyList<PublicSiteLinkDto> FooterBottomLinks,
    IReadOnlyList<PublicContactChannelDto> ContactPageChannels,
    IReadOnlyList<PublicContactOfficeDto> ContactPageOffices,
    IReadOnlyList<PublicContactWorkingHourDto> ContactPageWorkingHours,
    string ContactPageMapTitle,
    string ContactPageMapEmbedUrl,
    bool ContactPageMapIsVisible,
    IReadOnlyList<PublicManagedPageDto> Pages);
