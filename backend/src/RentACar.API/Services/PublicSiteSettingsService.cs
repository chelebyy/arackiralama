using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) : IPublicSiteSettingsService
{
    private const string SingletonKey = "public-site";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex SafeInternalPathRegex = new(@"^/[a-zA-Z0-9/_?=&.#-]*$", RegexOptions.Compiled);
    private static readonly Regex SafeSlugRegex = new(@"^[a-z0-9][a-z0-9-]{0,78}[a-z0-9]$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedSocialPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Instagram",
        "Facebook",
        "Twitter",
        "X",
        "YouTube",
        "LinkedIn",
        "WhatsApp"
    };
    private static readonly HashSet<string> AllowedContactChannelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone",
        "whatsapp",
        "email",
        "emergency"
    };
    private static readonly HashSet<string> AllowedContactOfficeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "main",
        "airport",
        "branch"
    };

    public async Task<PublicSiteSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return Map(settings);
    }

    public async Task<PublicSiteSettingsDto> UpdateAsync(UpdatePublicSiteSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var settings = await GetOrCreateAsync(cancellationToken);

        settings.CompanyName = normalized.CompanyName.Trim();
        settings.CompanyAddress = normalized.CompanyAddress.Trim();
        settings.CompanyPhone = normalized.CompanyPhone.Trim();
        settings.CompanyEmail = normalized.CompanyEmail.Trim();
        settings.WorkingHours = normalized.WorkingHours.Trim();
        settings.HeaderLinksJson = SerializeLinks(normalized.HeaderLinks);
        settings.HeroLinksJson = SerializeLinks(normalized.HeroLinks);
        settings.QuickLinksJson = SerializeLinks(normalized.QuickLinks);
        settings.SocialLinksJson = SerializeSocialLinks(normalized.SocialLinks);
        settings.FooterBottomLinksJson = SerializeLinks(normalized.FooterBottomLinks);
        settings.ContactPageChannelsJson = SerializeContactChannels(normalized.ContactPageChannels);
        settings.ContactPageOfficesJson = SerializeContactOffices(normalized.ContactPageOffices);
        settings.ContactPageWorkingHoursJson = SerializeContactWorkingHours(normalized.ContactPageWorkingHours);
        settings.ContactPageMapTitle = NormalizeText(normalized.ContactPageMapTitle, 160, "Office Locations Map");
        settings.ContactPageMapEmbedUrl = NormalizeMapEmbedUrl(normalized.ContactPageMapEmbedUrl);
        settings.ContactPageMapIsVisible = normalized.ContactPageMapIsVisible;
        settings.PagesJson = SerializePages(normalized.Pages);
        settings.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(settings);
    }

    private async Task<PublicSiteSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.PublicSiteSettings
            .FirstOrDefaultAsync(x => x.Key == SingletonKey, cancellationToken);

        if (settings is not null)
        {
            if (ShouldSeedNewLinkSections(settings))
            {
                settings.HeaderLinksJson = SerializeLinks(DefaultHeaderLinks());
                settings.HeroLinksJson = SerializeLinks(DefaultHeroLinks());
                settings.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (ShouldSeedContactPageSections(settings))
            {
                settings.ContactPageChannelsJson = SerializeContactChannels(DefaultContactPageChannels());
                settings.ContactPageOfficesJson = SerializeContactOffices(DefaultContactPageOffices());
                settings.ContactPageWorkingHoursJson = SerializeContactWorkingHours(DefaultContactPageWorkingHours());
                settings.ContactPageMapTitle = "Office Locations Map";
                settings.ContactPageMapEmbedUrl = DefaultContactPageMapEmbedUrl();
                settings.ContactPageMapIsVisible = true;
                settings.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (ShouldSeedPages(settings))
            {
                settings.PagesJson = SerializePages(DefaultPages());
                settings.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return settings;
        }

        settings = CreateDefaultSettings();
        dbContext.PublicSiteSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static PublicSiteSettings CreateDefaultSettings() => new()
    {
        Key = SingletonKey,
        CompanyName = "Alanya Car Rental",
        CompanyAddress = "Alanya, Antalya, Türkiye",
        CompanyPhone = "+90 555 555 01 00",
        CompanyEmail = "contact@alanyacarrental.com",
        WorkingHours = "08:00 - 22:00",
        HeaderLinksJson = SerializeLinks(DefaultHeaderLinks()),
        HeroLinksJson = SerializeLinks(DefaultHeroLinks()),
        QuickLinksJson = SerializeLinks(DefaultQuickLinks()),
        SocialLinksJson = SerializeSocialLinks(DefaultSocialLinks()),
        FooterBottomLinksJson = SerializeLinks(DefaultFooterBottomLinks()),
        ContactPageChannelsJson = SerializeContactChannels(DefaultContactPageChannels()),
        ContactPageOfficesJson = SerializeContactOffices(DefaultContactPageOffices()),
        ContactPageWorkingHoursJson = SerializeContactWorkingHours(DefaultContactPageWorkingHours()),
        ContactPageMapTitle = "Office Locations Map",
        ContactPageMapEmbedUrl = DefaultContactPageMapEmbedUrl(),
        ContactPageMapIsVisible = true,
        PagesJson = SerializePages(DefaultPages()),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static UpdatePublicSiteSettingsRequest Normalize(UpdatePublicSiteSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            throw new ArgumentException("Şirket adı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(request.CompanyEmail) || !request.CompanyEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Geçerli şirket e-posta adresi zorunludur.");
        }

        return request with
        {
            HeaderLinks = NormalizeLinks(request.HeaderLinks, maxCount: 8),
            HeroLinks = NormalizeLinks(request.HeroLinks, maxCount: 4),
            QuickLinks = NormalizeLinks(request.QuickLinks, maxCount: 12),
            SocialLinks = NormalizeSocialLinks(request.SocialLinks, maxCount: 8),
            FooterBottomLinks = NormalizeLinks(request.FooterBottomLinks, maxCount: 6),
            ContactPageChannels = NormalizeContactChannels(request.ContactPageChannels, maxCount: 8),
            ContactPageOffices = NormalizeContactOffices(request.ContactPageOffices, maxCount: 8),
            ContactPageWorkingHours = NormalizeContactWorkingHours(request.ContactPageWorkingHours, maxCount: 8),
            ContactPageMapTitle = NormalizeText(request.ContactPageMapTitle, 160, "Office Locations Map"),
            ContactPageMapEmbedUrl = NormalizeMapEmbedUrl(request.ContactPageMapEmbedUrl),
            Pages = NormalizePages(request.Pages, maxCount: 80)
        };
    }

    private static IReadOnlyList<PublicSiteLinkDto> NormalizeLinks(IReadOnlyList<PublicSiteLinkDto>? links, int maxCount)
    {
        return (links ?? [])
            .Take(maxCount)
            .Select((link, index) =>
            {
                var id = NormalizeId(link.Id, $"link-{index + 1}");
                var label = NormalizeText(link.Label, 80, "Bağlantı");
                var href = NormalizeInternalHref(link.Href);
                return link with { Id = id, Label = label, Href = href, SortOrder = index };
            })
            .ToList();
    }

    private static IReadOnlyList<PublicSocialLinkDto> NormalizeSocialLinks(IReadOnlyList<PublicSocialLinkDto>? links, int maxCount)
    {
        return (links ?? [])
            .Take(maxCount)
            .Select((link, index) =>
            {
                var platform = NormalizeText(link.Platform, 40, "Social");
                if (!AllowedSocialPlatforms.Contains(platform))
                {
                    throw new ArgumentException($"Desteklenmeyen sosyal medya platformu: {platform}");
                }

                return link with
                {
                    Id = NormalizeId(link.Id, platform.ToLowerInvariant()),
                    Platform = platform,
                    Url = NormalizeExternalUrl(link.Url),
                    SortOrder = index
                };
            })
            .ToList();
    }

    private static IReadOnlyList<PublicContactChannelDto> NormalizeContactChannels(IReadOnlyList<PublicContactChannelDto>? channels, int maxCount)
    {
        return (channels ?? [])
            .Take(maxCount)
            .Select((channel, index) =>
            {
                var type = NormalizeText(channel.Type, 30, "phone").ToLowerInvariant();
                if (!AllowedContactChannelTypes.Contains(type))
                {
                    throw new ArgumentException($"Desteklenmeyen iletişim kanal tipi: {type}");
                }

                return channel with
                {
                    Id = NormalizeId(channel.Id, $"contact-{index + 1}"),
                    Type = type,
                    Label = NormalizeText(channel.Label, 80, "İletişim"),
                    Value = NormalizeText(channel.Value, 120, "-"),
                    Href = NormalizeContactHref(channel.Href, type),
                    Description = NormalizeText(channel.Description, 180, string.Empty),
                    SortOrder = index
                };
            })
            .ToList();
    }

    private static IReadOnlyList<PublicContactOfficeDto> NormalizeContactOffices(IReadOnlyList<PublicContactOfficeDto>? offices, int maxCount)
    {
        return (offices ?? [])
            .Take(maxCount)
            .Select((office, index) =>
            {
                var type = NormalizeText(office.Type, 30, "branch").ToLowerInvariant();
                if (!AllowedContactOfficeTypes.Contains(type))
                {
                    throw new ArgumentException($"Desteklenmeyen ofis tipi: {type}");
                }

                return office with
                {
                    Id = NormalizeId(office.Id, $"office-{index + 1}"),
                    Name = NormalizeText(office.Name, 120, "Ofis"),
                    Address = NormalizeText(office.Address, 240, "-"),
                    Phone = NormalizeText(office.Phone, 80, "-"),
                    Hours = NormalizeText(office.Hours, 80, "-"),
                    Type = type,
                    SortOrder = index
                };
            })
            .ToList();
    }

    private static IReadOnlyList<PublicContactWorkingHourDto> NormalizeContactWorkingHours(IReadOnlyList<PublicContactWorkingHourDto>? rows, int maxCount)
    {
        return (rows ?? [])
            .Take(maxCount)
            .Select((row, index) => row with
            {
                Id = NormalizeId(row.Id, $"hours-{index + 1}"),
                Day = NormalizeText(row.Day, 80, "Gün"),
                Hours = NormalizeText(row.Hours, 80, "-"),
                SortOrder = index
            })
            .ToList();
    }

    private static IReadOnlyList<PublicManagedPageDto> NormalizePages(IReadOnlyList<PublicManagedPageDto>? pages, int maxCount)
    {
        return (pages ?? [])
            .Take(maxCount)
            .Select((page, index) =>
            {
                var slug = NormalizeSlug(page.Slug);
                var locale = NormalizeText(page.Locale, 12, "tr").ToLowerInvariant();
                return page with
                {
                    Id = NormalizeId(page.Id, $"{locale}-{slug}"),
                    Slug = slug,
                    Locale = locale,
                    Title = NormalizeText(page.Title, 160, "Sayfa"),
                    Subtitle = NormalizeText(page.Subtitle, 300, string.Empty),
                    SeoTitle = NormalizeText(page.SeoTitle, 160, page.Title),
                    SeoDescription = NormalizeText(page.SeoDescription, 300, page.Subtitle),
                    SortOrder = index,
                    Blocks = NormalizePageBlocks(page.Blocks, maxCount: 24)
                };
            })
            .GroupBy(page => $"{page.Locale}:{page.Slug}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<PublicPageBlockDto> NormalizePageBlocks(IReadOnlyList<PublicPageBlockDto>? blocks, int maxCount)
    {
        return (blocks ?? [])
            .Take(maxCount)
            .Select((block, index) => block with
            {
                Id = NormalizeId(block.Id, $"block-{index + 1}"),
                Heading = NormalizeText(block.Heading, 160, "Bölüm"),
                Body = NormalizeText(block.Body, 5000, string.Empty),
                SortOrder = index
            })
            .ToList();
    }

    private static string NormalizeSlug(string slug)
    {
        var value = NormalizeText(slug, 80, "sayfa").ToLowerInvariant();
        value = Regex.Replace(value, @"[^a-z0-9-]", "-");
        value = Regex.Replace(value, "-{2,}", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(value) || !SafeSlugRegex.IsMatch(value))
        {
            throw new ArgumentException("Sayfa slug değeri küçük harf, rakam ve tire içermelidir.");
        }

        return value;
    }

    private static string NormalizeInternalHref(string href)
    {
        var value = NormalizeText(href, 160, "/");
        if (!SafeInternalPathRegex.IsMatch(value) || value.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("Public bağlantılar site içi güvenli bir yol olmalıdır.");
        }

        return value;
    }

    private static string NormalizeExternalUrl(string url)
    {
        var value = NormalizeText(url, 240, "https://example.com");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Sosyal medya URL'i http veya https olmalıdır.");
        }

        return uri.ToString();
    }

    private static string NormalizeContactHref(string href, string type)
    {
        var value = NormalizeText(href, 240, type == "email" ? "mailto:contact@example.com" : "tel:+905555550100");
        if (value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return NormalizeInternalHref(value);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("İletişim bağlantısı tel:, mailto:, http(s) veya site içi yol olmalıdır.");
        }

        return uri.ToString();
    }

    private static string NormalizeMapEmbedUrl(string url)
    {
        var value = NormalizeText(url, 1200, DefaultContactPageMapEmbedUrl());
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !IsGoogleHost(uri.Host) ||
            !uri.AbsolutePath.StartsWith("/maps/embed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Harita bağlantısı geçerli bir Google Maps embed URL'i olmalıdır.");
        }

        return uri.ToString();
    }

    private static bool IsGoogleHost(string host) =>
        host.Equals("google.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeId(string id, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(id) ? fallback : id.Trim();
        return Regex.Replace(value, @"[^a-zA-Z0-9_-]", "-")[..Math.Min(value.Length, 60)];
    }

    private static string NormalizeText(string value, int maxLength, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }

    private static string SerializeLinks(IReadOnlyList<PublicSiteLinkDto> links) =>
        JsonSerializer.Serialize(links.OrderBy(x => x.SortOrder), JsonOptions);

    private static string SerializeSocialLinks(IReadOnlyList<PublicSocialLinkDto> links) =>
        JsonSerializer.Serialize(links.OrderBy(x => x.SortOrder), JsonOptions);

    private static string SerializeContactChannels(IReadOnlyList<PublicContactChannelDto> channels) =>
        JsonSerializer.Serialize(channels.OrderBy(x => x.SortOrder), JsonOptions);

    private static string SerializeContactOffices(IReadOnlyList<PublicContactOfficeDto> offices) =>
        JsonSerializer.Serialize(offices.OrderBy(x => x.SortOrder), JsonOptions);

    private static string SerializeContactWorkingHours(IReadOnlyList<PublicContactWorkingHourDto> rows) =>
        JsonSerializer.Serialize(rows.OrderBy(x => x.SortOrder), JsonOptions);

    private static string SerializePages(IReadOnlyList<PublicManagedPageDto> pages) =>
        JsonSerializer.Serialize(pages.OrderBy(x => x.SortOrder), JsonOptions);

    private static IReadOnlyList<PublicSiteLinkDto> DeserializeLinks(string json, IReadOnlyList<PublicSiteLinkDto> fallback)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<PublicSiteLinkDto>>(json, JsonOptions) ?? fallback;
    }

    private static IReadOnlyList<PublicSocialLinkDto> DeserializeSocialLinks(string json, IReadOnlyList<PublicSocialLinkDto> fallback)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<PublicSocialLinkDto>>(json, JsonOptions) ?? fallback;
    }

    private static IReadOnlyList<PublicContactChannelDto> DeserializeContactChannels(string json, IReadOnlyList<PublicContactChannelDto> fallback)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<PublicContactChannelDto>>(json, JsonOptions) ?? fallback;
    }

    private static IReadOnlyList<PublicContactOfficeDto> DeserializeContactOffices(string json, IReadOnlyList<PublicContactOfficeDto> fallback)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<PublicContactOfficeDto>>(json, JsonOptions) ?? fallback;
    }

    private static IReadOnlyList<PublicContactWorkingHourDto> DeserializeContactWorkingHours(string json, IReadOnlyList<PublicContactWorkingHourDto> fallback)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<PublicContactWorkingHourDto>>(json, JsonOptions) ?? fallback;
    }

    private static IReadOnlyList<PublicManagedPageDto> DeserializePages(string json, IReadOnlyList<PublicManagedPageDto> fallback)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<PublicManagedPageDto>>(json, JsonOptions) ?? fallback;
    }

    private static bool ShouldSeedNewLinkSections(PublicSiteSettings settings) =>
        IsEmptyJsonArray(settings.HeaderLinksJson) &&
        IsEmptyJsonArray(settings.HeroLinksJson) &&
        settings.CreatedAt == settings.UpdatedAt;

    private static bool ShouldSeedContactPageSections(PublicSiteSettings settings) =>
        IsEmptyJsonArray(settings.ContactPageChannelsJson) &&
        IsEmptyJsonArray(settings.ContactPageOfficesJson) &&
        IsEmptyJsonArray(settings.ContactPageWorkingHoursJson);

    private static bool ShouldSeedPages(PublicSiteSettings settings) =>
        IsEmptyJsonArray(settings.PagesJson);

    private static bool IsEmptyJsonArray(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("[]", StringComparison.Ordinal);

    private static PublicSiteSettingsDto Map(PublicSiteSettings settings) => new(
        settings.CompanyName,
        settings.CompanyAddress,
        settings.CompanyPhone,
        settings.CompanyEmail,
        settings.WorkingHours,
        DeserializeLinks(settings.HeaderLinksJson, DefaultHeaderLinks()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeLinks(settings.HeroLinksJson, DefaultHeroLinks()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeLinks(settings.QuickLinksJson, DefaultQuickLinks()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeSocialLinks(settings.SocialLinksJson, DefaultSocialLinks()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeLinks(settings.FooterBottomLinksJson, DefaultFooterBottomLinks()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeContactChannels(settings.ContactPageChannelsJson, DefaultContactPageChannels()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeContactOffices(settings.ContactPageOfficesJson, DefaultContactPageOffices()).OrderBy(x => x.SortOrder).ToList(),
        DeserializeContactWorkingHours(settings.ContactPageWorkingHoursJson, DefaultContactPageWorkingHours()).OrderBy(x => x.SortOrder).ToList(),
        settings.ContactPageMapTitle,
        settings.ContactPageMapEmbedUrl,
        settings.ContactPageMapIsVisible,
        DeserializePages(settings.PagesJson, DefaultPages()).OrderBy(x => x.SortOrder).ToList(),
        settings.UpdatedAt);

    private static IReadOnlyList<PublicSiteLinkDto> DefaultHeaderLinks() =>
    [
        new("home", "Ana Sayfa", "/", true, 0),
        new("vehicles", "Araçlar", "/vehicles", true, 1),
        new("about", "Hakkımızda", "/about", true, 2),
        new("contact", "İletişim", "/contact", true, 3),
        new("login", "Giriş Yap", "/dashboard/login/v2", true, 4),
        new("trackReservation", "Rezervasyon Takip", "/track-reservation", true, 5)
    ];

    private static IReadOnlyList<PublicSiteLinkDto> DefaultHeroLinks() =>
    [
        new("ctaPrimary", "Araçları İncele", "/vehicles", true, 0),
        new("ctaSecondary", "Rezervasyon Yap", "/booking", true, 1)
    ];

    private static IReadOnlyList<PublicSiteLinkDto> DefaultQuickLinks() =>
    [
        new("vehicles", "Araçlar", "/vehicles", true, 0),
        new("howItWorks", "Nasıl Çalışır", "/about", true, 1),
        new("contact", "İletişim", "/contact", true, 2),
        new("track", "Rezervasyon Takip", "/track-reservation", true, 3),
        new("booking", "Rezervasyon", "/booking", true, 4),
        new("terms", "Kullanım Koşulları", "/terms", true, 5),
        new("privacy", "Gizlilik Politikası", "/privacy", true, 6)
    ];

    private static IReadOnlyList<PublicSocialLinkDto> DefaultSocialLinks() =>
    [
        new("instagram", "Instagram", "https://instagram.com", true, 0),
        new("facebook", "Facebook", "https://facebook.com", true, 1),
        new("twitter", "Twitter", "https://twitter.com", true, 2)
    ];

    private static IReadOnlyList<PublicSiteLinkDto> DefaultFooterBottomLinks() =>
    [
        new("howItWorks", "Nasıl Çalışır", "/about", true, 0),
        new("contact", "İletişim", "/contact", true, 1)
    ];

    private static IReadOnlyList<PublicContactChannelDto> DefaultContactPageChannels() =>
    [
        new("reservations", "phone", "Rezervasyon", "+90 242 555 10 00", "tel:+902425551000", "Rezervasyon ve araç müsaitliği için arayın.", true, 0),
        new("whatsapp", "whatsapp", "WhatsApp", "+90 555 123 45 67", "https://wa.me/905551234567", "Hızlı teklif ve destek için yazın.", true, 1),
        new("email", "email", "Email", "info@alanyacarrental.com", "mailto:info@alanyacarrental.com", "Rezervasyon ve genel sorular için email gönderin.", true, 2),
        new("emergency", "emergency", "Acil Durum", "+90 555 999 00 00", "tel:+905559990000", "Aktif kiralama sürecinde 7/24 acil destek.", true, 3)
    ];

    private static IReadOnlyList<PublicContactOfficeDto> DefaultContactPageOffices() =>
    [
        new("main", "Ana Ofis - Alanya Merkez", "Alanya merkez teslimat noktası", "+90 242 555 10 00", "08:00 - 20:00", "main", true, 0),
        new("gzp", "Gazipaşa Havalimanı Desk", "Gazipaşa-Alanya Havalimanı", "+90 242 555 10 01", "24/7", "airport", true, 1),
        new("ayt", "Antalya Havalimanı Desk", "Antalya Havalimanı", "+90 242 555 10 02", "24/7", "airport", true, 2),
        new("mahmutlar", "Mahmutlar Ofis", "Mahmutlar teslimat noktası", "+90 242 555 10 03", "09:00 - 19:00", "branch", true, 3)
    ];

    private static IReadOnlyList<PublicContactWorkingHourDto> DefaultContactPageWorkingHours() =>
    [
        new("mondayFriday", "Pazartesi - Cuma", "08:00 - 20:00", true, 0),
        new("saturday", "Cumartesi", "09:00 - 18:00", true, 1),
        new("sunday", "Pazar", "10:00 - 16:00", true, 2),
        new("holidays", "Resmi Tatiller", "10:00 - 16:00", true, 3)
    ];

    private static IReadOnlyList<PublicManagedPageDto> DefaultPages() =>
    [
        new(
            "tr-about",
            "about",
            "tr",
            "Hakkımızda",
            "Alanya'da güvenilir araç kiralama deneyimi.",
            "Hakkımızda | Alanya Car Rental",
            "Alanya Car Rental hakkında bilgi alın.",
            true,
            0,
            [
                new("story", "Hikayemiz", "Alanya Car Rental, turistler ve yerel müşteriler için güvenilir, şeffaf ve hızlı araç kiralama deneyimi sunar.", true, 0),
                new("service", "Hizmet Anlayışımız", "Havalimanı teslimatı, net fiyatlandırma ve hızlı destek süreçleriyle rezervasyondan teslimata kadar sade bir deneyim hedefleriz.", true, 1)
            ]),
        new(
            "tr-terms",
            "terms",
            "tr",
            "Kullanım Koşulları",
            "Rezervasyon ve kiralama sürecine ilişkin temel koşullar.",
            "Kullanım Koşulları | Alanya Car Rental",
            "Araç kiralama kullanım koşulları ve rezervasyon kuralları.",
            true,
            1,
            [
                new("agreement", "Sözleşme", "Rezervasyon oluşturan müşteri, kiralama sözleşmesi ve teslimat kurallarını kabul etmiş sayılır.", true, 0),
                new("payment", "Ödeme ve İptal", "Ödeme, depozito, iptal ve iade süreçleri rezervasyon ekranında gösterilen koşullara göre yürütülür.", true, 1)
            ]),
        new(
            "tr-privacy",
            "privacy",
            "tr",
            "Gizlilik Politikası",
            "Kişisel verilerin işlenmesi ve saklanmasına ilişkin bilgiler.",
            "Gizlilik Politikası | Alanya Car Rental",
            "Kişisel veri ve gizlilik politikası.",
            true,
            2,
            [
                new("collection", "Toplanan Bilgiler", "Rezervasyon, iletişim ve ödeme süreçleri için gerekli müşteri ve rezervasyon bilgileri işlenir.", true, 0),
                new("rights", "Haklarınız", "Kişisel verilerinizle ilgili bilgi alma, düzeltme ve silme taleplerinizi iletişim kanallarımız üzerinden iletebilirsiniz.", true, 1)
            ])
    ];

    private static string DefaultContactPageMapEmbedUrl() =>
        "https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d128084.037171682!2d31.95928245!3d36.54115!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x14dca27b8223b0b7%3A0x403b37d0ec0cb80!2sAlanya%2C%20Antalya%2C%20Turkey!5e0!3m2!1sen!2sus!4v1700000000000!5m2!1sen!2sus";
}
