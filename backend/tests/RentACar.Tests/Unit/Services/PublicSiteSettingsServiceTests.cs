using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Infrastructure.Data;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class PublicSiteSettingsServiceTests
{
    [Fact]
    public async Task GetAdminContentAsync_returns_versioned_page_content()
    {
        await using var dbContext = CreateDbContext();
        var service = new PublicSiteSettingsService(dbContext);

        var content = await service.GetAdminContentAsync();

        content.Version.Should().NotBeNullOrWhiteSpace();
        content.Pages.Should().Contain(page => page.Slug == "privacy" && page.Locale == "tr");
    }

    [Fact]
    public async Task GetAsync_WhenMissing_CreatesDefaultSettings()
    {
        await using var dbContext = CreateDbContext();
        var service = new PublicSiteSettingsService(dbContext);

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
        var service = new PublicSiteSettingsService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.CompanyName.Should().Be("Dvn rent a car");
        dbContext.PublicSiteSettings.Single().CompanyName.Should().Be("Dvn rent a car");
    }

    [Fact]
    public async Task GetAsync_WhenPaymentMethodFlagsMissing_UsesSafeDefaults()
    {
        await using var dbContext = CreateDbContext();
        var service = new PublicSiteSettingsService(dbContext);

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
        var service = new PublicSiteSettingsService(dbContext);

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
        var service = new PublicSiteSettingsService(dbContext);

        var settings = await service.GetAsync(CancellationToken.None);

        settings.PaymentMethods.CreditCardEnabled.Should().BeFalse();
        settings.PaymentMethods.DebitCardEnabled.Should().BeFalse();
        settings.PaymentMethods.UnpaidRequestEnabled.Should().BeFalse();
        settings.PaymentMethods.AnyEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_NormalizesAndPersistsManagedLinks()
    {
        await using var dbContext = CreateDbContext();
        var service = new PublicSiteSettingsService(dbContext);
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
    public async Task UpdateAsync_RejectsUnsupportedPublicSettingTranslationLocale()
    {
        await using var dbContext = CreateDbContext();
        var service = new PublicSiteSettingsService(dbContext);
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
        var service = new PublicSiteSettingsService(dbContext);
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
        var service = new PublicSiteSettingsService(dbContext);
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
}
