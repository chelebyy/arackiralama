namespace RentACar.API.Contracts.PublicSiteSettings;

public sealed record PublicSiteLinkDto(
    string Id,
    string Label,
    string Href,
    bool IsVisible,
    int SortOrder);

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
    int SortOrder);

public sealed record PublicContactOfficeDto(
    string Id,
    string Name,
    string Address,
    string Phone,
    string Hours,
    string Type,
    bool IsVisible,
    int SortOrder);

public sealed record PublicContactWorkingHourDto(
    string Id,
    string Day,
    string Hours,
    bool IsVisible,
    int SortOrder);

public sealed record PublicPageBlockDto(
    string Id,
    string Heading,
    string Body,
    bool IsVisible,
    int SortOrder);

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
