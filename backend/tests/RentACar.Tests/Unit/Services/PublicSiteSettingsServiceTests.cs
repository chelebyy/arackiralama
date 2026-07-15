using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Data;
using RentACar.Infrastructure.Services.Payments;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PublicSiteSettingsServiceTests
{
    [Fact]
    public async Task GetAdminContentAsync_returns_versioned_page_content()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var content = await service.GetAdminContentAsync();

        content.Version.Should().NotBeNullOrWhiteSpace();
        content.Pages.Should().Contain(page => page.Slug == "privacy" && page.Locale == "tr");
    }

    [Fact]
    public async Task AdminContentVersion_UsesMicrosecondPrecisionForPostgresRoundTripStability()
    {
        await using var dbContext = CreateDbContext();
        var storedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var settings = CreateSettingsWithPages("[]");
        dbContext.PublicSiteSettings.Add(settings);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        await service.GetAdminContentAsync();
        settings.UpdatedAt = storedAt;
        await dbContext.SaveChangesAsync();
        var expectedVersion = (storedAt.Ticks - storedAt.Ticks % 10).ToString();

        var content = await service.GetAdminContentAsync();
        var act = () => service.UpdateContactContentAsync(
            new UpdateAdminPublicContactRequest(
                expectedVersion,
                [],
                [],
                [],
                "Map",
                "https://www.google.com/maps/embed?pb=managed",
                true),
            CancellationToken.None);

        content.Version.Should().Be(expectedVersion);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdatePageDraftAsync_does_not_change_public_page_until_publish()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var before = await service.GetAsync();
        var version = (await service.GetAdminContentAsync()).Version;

        await service.UpdatePageDraftAsync(
            "privacy",
            "tr",
            new UpdateAdminPublicPageDraftRequest(
                version,
                "Taslak Gizlilik",
                "Taslak alt başlık",
                "Taslak SEO",
                "Taslak SEO açıklaması",
                true,
                0,
                [
                    new PublicPageBlockDto("block-1", "Taslak Bölüm", "<p>Taslak içerik</p>", true, 0, "html")
                ]),
            CancellationToken.None);

        var publicAfterDraft = await service.GetAsync();

        publicAfterDraft.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr")
            .Title.Should().Be(before.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr").Title);
    }

    [Fact]
    public async Task UpdatePageDraftAsync_WhenLegacyPublishedPageHasNoSnapshot_DoesNotLeakDraft()
    {
        await using var dbContext = CreateDbContext();
        var publishedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var pagesJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "tr-privacy",
                slug = "privacy",
                locale = "tr",
                title = "Legacy Privacy",
                subtitle = "Legacy subtitle",
                seoTitle = "Legacy SEO",
                seoDescription = "Legacy description",
                isPublished = true,
                sortOrder = 0,
                blocks = new[]
                {
                    new { id = "legacy", heading = "Legacy Heading", body = "Legacy body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
                },
                published = (object?)null,
                draftUpdatedAtUtc = (DateTime?)null,
                publishedAtUtc = (DateTime?)publishedAt
            }
        });
        dbContext.PublicSiteSettings.Add(CreateSettingsWithPages(pagesJson));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var version = (await service.GetAdminContentAsync()).Version;

        var adminAfterDraft = await service.UpdatePageDraftAsync(
            "privacy",
            "tr",
            new UpdateAdminPublicPageDraftRequest(
                version,
                "Draft Privacy",
                "Draft subtitle",
                "Draft SEO",
                "Draft description",
                true,
                0,
                [new PublicPageBlockDto("draft", "Draft Heading", "Draft body", true, 0, "plain")]),
            CancellationToken.None);
        var publicAfterDraft = await service.GetAsync();

        var publicPage = publicAfterDraft.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
        publicPage.Title.Should().Be("Legacy Privacy");
        publicPage.Blocks.Single().Body.Should().Be("Legacy body");
        var adminPage = adminAfterDraft.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
        adminPage.Title.Should().Be("Draft Privacy");
        adminPage.Published.Should().NotBeNull();
        adminPage.Published!.Title.Should().Be("Legacy Privacy");
        adminPage.PublishedAtUtc.Should().Be(publishedAt);

        await service.PublishPageAsync("privacy", "tr", new PublishAdminPublicPageRequest(adminAfterDraft.Version), CancellationToken.None);
        var publicAfterPublish = await service.GetAsync();

        publicAfterPublish.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr")
            .Title.Should().Be("Draft Privacy");
    }

    [Fact]
    public async Task GetAsync_WhenDraftOnlyPageHasNoPublishedSnapshot_DoesNotExposeDraftContent()
    {
        await using var dbContext = CreateDbContext();
        var draftAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);
        var pagesJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "tr-privacy",
                slug = "privacy",
                locale = "tr",
                title = "Secret Draft Privacy",
                subtitle = "Secret draft subtitle",
                seoTitle = "Secret draft SEO",
                seoDescription = "Secret draft description",
                isPublished = false,
                sortOrder = 0,
                blocks = new[]
                {
                    new { id = "secret", heading = "Secret Heading", body = "Secret draft body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
                },
                published = (object?)null,
                draftUpdatedAtUtc = (DateTime?)draftAt,
                publishedAtUtc = (DateTime?)null
            }
        });
        dbContext.PublicSiteSettings.Add(CreateSettingsWithPages(pagesJson));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var publicPage = (await service.GetAsync()).Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");

        publicPage.IsPublished.Should().BeFalse();
        publicPage.Title.Should().BeEmpty();
        publicPage.Subtitle.Should().BeEmpty();
        publicPage.SeoTitle.Should().BeEmpty();
        publicPage.SeoDescription.Should().BeEmpty();
        publicPage.Blocks.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishPageAsync_promotes_draft_to_public_page()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var version = (await service.GetAdminContentAsync()).Version;

        var afterDraft = await service.UpdatePageDraftAsync(
            "privacy",
            "tr",
            new UpdateAdminPublicPageDraftRequest(
                version,
                "Yayınlanacak Gizlilik",
                "Yeni alt başlık",
                "Yeni SEO",
                "Yeni SEO açıklaması",
                true,
                0,
                [
                    new PublicPageBlockDto("block-1", "Yeni Bölüm", "<p>Yeni içerik</p>", true, 0, "html")
                ]),
            CancellationToken.None);

        await service.PublishPageAsync("privacy", "tr", new PublishAdminPublicPageRequest(afterDraft.Version), CancellationToken.None);

        var publicAfterPublish = await service.GetAsync();
        var page = publicAfterPublish.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");

        page.Title.Should().Be("Yayınlanacak Gizlilik");
        page.Blocks.Single().BodyFormat.Should().Be("html");
    }

    [Fact]
    public async Task UnpublishPageAsync_preserves_last_published_snapshot()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var publicBefore = await service.GetAsync();
        var version = (await service.GetAdminContentAsync()).Version;

        var adminAfterUnpublish = await service.UnpublishPageAsync(
            "privacy",
            "tr",
            new PublishAdminPublicPageRequest(version),
            CancellationToken.None);

        var page = adminAfterUnpublish.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
        page.IsPublished.Should().BeFalse();
        page.Published.Should().NotBeNull();
        page.Published!.Title.Should().Be(publicBefore.Pages.Single(publicPage => publicPage.Slug == "privacy" && publicPage.Locale == "tr").Title);
        page.PublishedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePageDraftAsync_rejects_stale_version()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var act = () => service.UpdatePageDraftAsync(
            "privacy",
            "tr",
            new UpdateAdminPublicPageDraftRequest(
                "1",
                "Stale",
                "",
                "",
                "",
                true,
                0,
                [new PublicPageBlockDto("block-1", "Bölüm", "İçerik", true, 0, "plain")]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Public content was updated by another session. Reload before saving.");
    }

    [Fact]
    public async Task GetAsync_WhenMissing_CreatesDefaultSettings()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.CompanyName.Should().Be("Dvn rent a car");
        settings.HeaderLinks.Should().Contain(x => x.Id == "trackReservation" && x.IsVisible);
        settings.HeroLinks.Should().Contain(x => x.Id == "ctaPrimary" && x.IsVisible);
        settings.QuickLinks.Should().Contain(x => x.Id == "vehicles" && x.IsVisible);
        settings.ContactPageChannels.Should().Contain(x => x.Id == "reservations" && x.IsVisible);
        settings.ContactPageOffices.Should().Contain(x => x.Id == "main" && x.IsVisible);
        settings.ContactPageWorkingHours.Should().Contain(x => x.Id == "mondayFriday" && x.IsVisible);
        settings.ContactPageMapIsVisible.Should().BeTrue();
        settings.ContactPageMapEmbedUrl.Should().Contain("google.com/maps/embed");
        settings.Pages.Should().Contain(x => x.Slug == "terms" && x.IsPublished);
        dbContext.PublicSiteSettings.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAsync_WhenExistingSettingsUseLegacyDefaultBrand_UpdatesCompanyName()
    {
        await using var dbContext = CreateDbContext();
        dbContext.PublicSiteSettings.Add(new PublicSiteSettings
        {
            Key = "public-site",
            CompanyName = "Alanya Car Rental",
            CompanyAddress = "Alanya",
            CompanyPhone = "+90 555",
            CompanyEmail = "legacy@example.test",
            WorkingHours = "09:00 - 18:00",
            HeaderLinksJson = "[]",
            HeroLinksJson = "[]",
            QuickLinksJson = "[]",
            SocialLinksJson = "[]",
            FooterBottomLinksJson = "[]",
            ContactPageChannelsJson = "[]",
            ContactPageOfficesJson = "[]",
            ContactPageWorkingHoursJson = "[]",
            PagesJson = "[]",
            ContactPageMapTitle = "Map",
            ContactPageMapEmbedUrl = "https://www.google.com/maps/embed?pb=managed",
            ContactPageMapIsVisible = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.CompanyName.Should().Be("Dvn rent a car");
        dbContext.PublicSiteSettings.Single().CompanyName.Should().Be("Dvn rent a car");
    }

    [Fact]
    public async Task GetAsync_WhenPaymentMethodFlagsMissing_UsesSafeDefaults()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.PaymentMethods.CreditCardEnabled.Should().BeFalse();
        settings.PaymentMethods.DebitCardEnabled.Should().BeFalse();
        settings.PaymentMethods.UnpaidRequestEnabled.Should().BeTrue();
        settings.PaymentMethods.PaypalEnabled.Should().BeFalse();
        settings.PaymentMethods.AnyEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WhenOnlinePaymentDisabled_HidesCardMethodsButKeepsUnpaidFlag()
    {
        await using var dbContext = CreateDbContext();
        dbContext.FeatureFlags.AddRange(
            new FeatureFlag { Name = "EnableOnlinePayment", Enabled = false, Description = "test" },
            new FeatureFlag { Name = "EnableCreditCardPayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableDebitCardPayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableUnpaidReservationRequest", Enabled = true, Description = "test" });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.PaymentMethods.CreditCardEnabled.Should().BeFalse();
        settings.PaymentMethods.DebitCardEnabled.Should().BeFalse();
        settings.PaymentMethods.UnpaidRequestEnabled.Should().BeTrue();
        settings.PaymentMethods.AnyEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WhenOnlinePaymentAndUnpaidAreDisabled_ReportsNoActionableMethods()
    {
        await using var dbContext = CreateDbContext();
        dbContext.FeatureFlags.AddRange(
            new FeatureFlag { Name = "EnableOnlinePayment", Enabled = false, Description = "test" },
            new FeatureFlag { Name = "EnableCreditCardPayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableDebitCardPayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableUnpaidReservationRequest", Enabled = false, Description = "test" });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.PaymentMethods.CreditCardEnabled.Should().BeFalse();
        settings.PaymentMethods.DebitCardEnabled.Should().BeFalse();
        settings.PaymentMethods.UnpaidRequestEnabled.Should().BeFalse();
        settings.PaymentMethods.AnyEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GetAsync_WhenPaymentsDisabled_HidesCardMethodsAndPreservesUnpaidAvailability(
        bool unpaidRequestEnabled,
        bool anyMethodExpected)
    {
        await using var dbContext = CreateDbContext();
        dbContext.FeatureFlags.AddRange(
            new FeatureFlag { Name = "EnableOnlinePayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableCreditCardPayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableDebitCardPayment", Enabled = true, Description = "test" },
            new FeatureFlag { Name = "EnableUnpaidReservationRequest", Enabled = unpaidRequestEnabled, Description = "test" });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, enablePayments: false);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.PaymentMethods.CreditCardEnabled.Should().BeFalse();
        settings.PaymentMethods.DebitCardEnabled.Should().BeFalse();
        settings.PaymentMethods.UnpaidRequestEnabled.Should().Be(unpaidRequestEnabled);
        settings.PaymentMethods.AnyEnabled.Should().Be(anyMethodExpected);
        settings.OnlinePaymentEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_NormalizesAndPersistsManagedLinks()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdatePublicSiteSettingsRequest(
            " Managed Rent ",
            "Alanya",
            "+90 555",
            "managed@example.test",
            "09:00 - 18:00",
            [
                new PublicSiteLinkDto(
                    "home",
                    "Ana Sayfa",
                    "/",
                    true,
                    99,
                    new Dictionary<string, PublicLocalizedTextDto>
                    {
                        ["en"] = new(Label: "Home"),
                        ["de"] = new(Label: "  ")
                    })
            ],
            [new PublicSiteLinkDto("ctaSecondary", "Rezervasyon", "/booking", false, 50)],
            [
                new PublicSiteLinkDto("contact", "İletişim", "/contact", true, 99),
                new PublicSiteLinkDto("hidden", "Gizli", "/hidden", false, 100)
            ],
            [new PublicSocialLinkDto("instagram", "Instagram", "https://instagram.com/managed", true, 12)],
            [new PublicSiteLinkDto("bottom", "Alt", "/bottom", true, 20)],
            [
                new PublicContactChannelDto(
                    "phone",
                    "phone",
                    "Managed Phone",
                    "+90 555 000",
                    "tel:+90555000",
                    "Managed desc",
                    true,
                    4,
                    new Dictionary<string, PublicLocalizedTextDto>
                    {
                        ["en"] = new(Label: "Phone", Value: "+90 555 111", Description: "English desc")
                    })
            ],
            [
                new PublicContactOfficeDto(
                    "office",
                    "Managed Office",
                    "Managed address",
                    "+90 555",
                    "10:00 - 17:00",
                    "branch",
                    true,
                    5,
                    new Dictionary<string, PublicLocalizedTextDto>
                    {
                        ["en"] = new(Name: "Office", Address: "English address", Hours: "12:00 - 18:00")
                    })
            ],
            [
                new PublicContactWorkingHourDto(
                    "hours",
                    "Managed Day",
                    "11:00 - 16:00",
                    true,
                    6,
                    new Dictionary<string, PublicLocalizedTextDto>
                    {
                        ["en"] = new(Day: "Weekday", Hours: "13:00 - 17:00")
                    })
            ],
            "Managed Map",
            "https://www.google.com/maps/embed?pb=managed",
            false,
            [
                new PublicManagedPageDto(
                    "tr-terms",
                    "terms",
                    "TR",
                    "Managed Terms",
                    "Managed subtitle",
                    "Managed SEO",
                    "Managed description",
                    false,
                    10,
                    [new PublicPageBlockDto("body", "Managed Block", "Managed body", true, 2)])
            ]);

        var updated = await service.UpdateAsync(request, CancellationToken.None);
        var reloaded = await service.GetAsync(CancellationToken.None);

        updated.CompanyName.Should().Be("Managed Rent");
        var headerLink = reloaded.HeaderLinks.Should().ContainSingle(x => x.Href == "/").Subject;
        headerLink.Translations.Should().NotBeNull();
        headerLink.Translations!["en"].Label.Should().Be("Home");
        headerLink.Translations.Should().NotContainKey("de");
        reloaded.HeroLinks.Should().ContainSingle(x => x.Id == "ctaSecondary" && !x.IsVisible);
        reloaded.QuickLinks.Select(x => x.SortOrder).Should().Equal(0, 1);
        reloaded.SocialLinks.Should().ContainSingle(x => x.Url == "https://instagram.com/managed");
        reloaded.FooterBottomLinks.Should().ContainSingle(x => x.Href == "/bottom");
        var contactChannel = reloaded.ContactPageChannels.Should().ContainSingle(x => x.Label == "Managed Phone" && x.SortOrder == 0).Subject;
        contactChannel.Translations!["en"].Description.Should().Be("English desc");
        var office = reloaded.ContactPageOffices.Should().ContainSingle(x => x.Name == "Managed Office" && x.SortOrder == 0).Subject;
        office.Translations!["en"].Address.Should().Be("English address");
        var workingHour = reloaded.ContactPageWorkingHours.Should().ContainSingle(x => x.Day == "Managed Day" && x.SortOrder == 0).Subject;
        workingHour.Translations!["en"].Hours.Should().Be("13:00 - 17:00");
        reloaded.ContactPageMapTitle.Should().Be("Managed Map");
        reloaded.ContactPageMapEmbedUrl.Should().Be("https://www.google.com/maps/embed?pb=managed");
        reloaded.ContactPageMapIsVisible.Should().BeFalse();
        reloaded.Pages.Should().ContainSingle(x => x.Slug == "terms" && x.Locale == "tr" && !x.IsPublished);
        reloaded.Pages.Single().Blocks.Should().ContainSingle(x => x.SortOrder == 0);
    }

    [Fact]
    public async Task UpdateAsync_PreservesExistingPagePublicationMetadata()
    {
        await using var dbContext = CreateDbContext();
        var draftAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);
        var publishedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var pagesJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "tr-terms",
                slug = "terms",
                locale = "tr",
                title = "Stored Draft Terms",
                subtitle = "Stored draft subtitle",
                seoTitle = "Stored draft SEO",
                seoDescription = "Stored draft description",
                isPublished = false,
                sortOrder = 0,
                blocks = new[]
                {
                    new { id = "draft", heading = "Draft Heading", body = "Stored draft body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
                },
                published = new
                {
                    title = "Published Terms",
                    subtitle = "Published subtitle",
                    seoTitle = "Published SEO",
                    seoDescription = "Published description",
                    blocks = new[]
                    {
                        new { id = "published", heading = "Published Heading", body = "Published body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
                    },
                    publishedAtUtc = publishedAt
                },
                draftUpdatedAtUtc = (DateTime?)draftAt,
                publishedAtUtc = (DateTime?)publishedAt
            }
        });
        dbContext.PublicSiteSettings.Add(CreateSettingsWithPages(pagesJson));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var request = CreateSettingsRequest(
        [
            new PublicManagedPageDto(
                "tr-terms",
                "terms",
                "TR",
                "Settings Draft Terms",
                "Settings draft subtitle",
                "Settings draft SEO",
                "Settings draft description",
                false,
                0,
                [new PublicPageBlockDto("settings-draft", "Settings Draft Heading", "Settings draft body", true, 0)])
        ]);

        await service.UpdateAsync(request, CancellationToken.None);

        var adminAfterUpdate = await service.GetAdminContentAsync();
        var adminPage = adminAfterUpdate.Pages.Single(page => page.Slug == "terms" && page.Locale == "tr");
        adminPage.Title.Should().Be("Stored Draft Terms");
        adminPage.Blocks.Single().Body.Should().Be("Stored draft body");
        adminPage.Published.Should().NotBeNull();
        adminPage.Published!.Title.Should().Be("Published Terms");
        adminPage.Published.Blocks.Single().Body.Should().Be("Published body");
        adminPage.DraftUpdatedAtUtc.Should().Be(draftAt);
        adminPage.PublishedAtUtc.Should().Be(publishedAt);
        var publicPage = (await service.GetAsync()).Pages.Single(page => page.Slug == "terms" && page.Locale == "tr");
        publicPage.Title.Should().Be("Published Terms");
        publicPage.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnsupportedPublicSettingTranslationLocale()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdatePublicSiteSettingsRequest(
            "Managed Rent",
            "Alanya",
            "+90 555",
            "managed@example.test",
            "09:00 - 18:00",
            [
                new PublicSiteLinkDto(
                    "home",
                    "Ana Sayfa",
                    "/",
                    true,
                    0,
                    new Dictionary<string, PublicLocalizedTextDto>
                    {
                        ["es"] = new(Label: "Inicio")
                    })
            ],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            "Map",
            "https://www.google.com/maps/embed?pb=managed",
            true,
            []);

        await service.Invoking(s => s.UpdateAsync(request, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Desteklenmeyen public site dili*");
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnsafeInternalHref()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdatePublicSiteSettingsRequest(
            "Managed Rent",
            "Alanya",
            "+90 555",
            "managed@example.test",
            "09:00 - 18:00",
            [],
            [],
            [new PublicSiteLinkDto("bad", "Bad", "javascript:alert(1)", true, 0)],
            [],
            [],
            [],
            [],
            [],
            "Map",
            "https://www.google.com/maps/embed?pb=managed",
            true,
            []);

        await service.Invoking(s => s.UpdateAsync(request, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*site içi*");
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnsafeMapEmbedUrl()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new UpdatePublicSiteSettingsRequest(
            "Managed Rent",
            "Alanya",
            "+90 555",
            "managed@example.test",
            "09:00 - 18:00",
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            "Map",
            "https://evilgoogle.com/maps/embed",
            true,
            []);

        await service.Invoking(s => s.UpdateAsync(request, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Google Maps embed*");
    }

    private static RentACarDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RentACarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new RentACarDbContext(options);
    }

    private static PublicSiteSettingsService CreateService(
        RentACarDbContext dbContext,
        bool enablePayments = true) =>
        new(
            dbContext,
            Options.Create(new PaymentOptions { EnablePayments = enablePayments }));

    private static PublicSiteSettings CreateSettingsWithPages(string pagesJson)
    {
        var now = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        return new PublicSiteSettings
        {
            Key = "public-site",
            CompanyName = "Dvn rent a car",
            CompanyAddress = "Alanya",
            CompanyPhone = "+90 555",
            CompanyEmail = "info@example.test",
            WorkingHours = "09:00 - 18:00",
            HeaderLinksJson = "[]",
            HeroLinksJson = "[]",
            QuickLinksJson = "[]",
            SocialLinksJson = "[]",
            FooterBottomLinksJson = "[]",
            ContactPageChannelsJson = "[]",
            ContactPageOfficesJson = "[]",
            ContactPageWorkingHoursJson = "[]",
            ContactPageMapTitle = "Map",
            ContactPageMapEmbedUrl = "https://www.google.com/maps/embed?pb=managed",
            ContactPageMapIsVisible = true,
            PagesJson = pagesJson,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static UpdatePublicSiteSettingsRequest CreateSettingsRequest(IReadOnlyList<PublicManagedPageDto> pages) => new(
        "Managed Rent",
        "Alanya",
        "+90 555",
        "managed@example.test",
        "09:00 - 18:00",
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        "Map",
        "https://www.google.com/maps/embed?pb=managed",
        true,
        pages);
}
