# Task 1 Review Package

Base: f0fb79d4c68f87489545cbc3d76bc5ae8021ca10
Head: c770f5a8ba9d76fb929d469e89bd9c52bd7802f1

## Commit Log
```
c770f5a feat(admin): add public content contracts
```

## Diff Stat
```
 .../PublicSiteSettings/PublicSiteSettingsDtos.cs   | 55 ++++++++++++++++
 .../Services/IPublicSiteSettingsService.cs         |  5 ++
 .../Services/PublicSiteSettingsService.cs          | 76 ++++++++++++++++++++++
 .../Services/PublicSiteSettingsServiceTests.cs     | 12 ++++
 4 files changed, 148 insertions(+)
```

## Diff
```diff
diff --git a/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs b/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
index 761499c..2149ceb 100644
--- a/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
+++ b/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
@@ -66,20 +66,75 @@ public sealed record PublicManagedPageDto(
     string Slug,
     string Locale,
     string Title,
     string Subtitle,
     string SeoTitle,
     string SeoDescription,
     bool IsPublished,
     int SortOrder,
     IReadOnlyList<PublicPageBlockDto> Blocks);
 
+public sealed record PublicPagePublishedSnapshotDto(
+    string Title,
+    string Subtitle,
+    string SeoTitle,
+    string SeoDescription,
+    IReadOnlyList<PublicPageBlockDto> Blocks,
+    DateTime PublishedAtUtc);
+
+public sealed record AdminPublicManagedPageDto(
+    string Id,
+    string Slug,
+    string Locale,
+    string Title,
+    string Subtitle,
+    string SeoTitle,
+    string SeoDescription,
+    bool IsPublished,
+    int SortOrder,
+    IReadOnlyList<PublicPageBlockDto> Blocks,
+    PublicPagePublishedSnapshotDto? Published,
+    DateTime? DraftUpdatedAtUtc,
+    DateTime? PublishedAtUtc);
+
+public sealed record AdminPublicContentDto(
+    string Version,
+    DateTime UpdatedAt,
+    IReadOnlyList<AdminPublicManagedPageDto> Pages,
+    IReadOnlyList<PublicContactChannelDto> ContactPageChannels,
+    IReadOnlyList<PublicContactOfficeDto> ContactPageOffices,
+    IReadOnlyList<PublicContactWorkingHourDto> ContactPageWorkingHours,
+    string ContactPageMapTitle,
+    string ContactPageMapEmbedUrl,
+    bool ContactPageMapIsVisible);
+
+public sealed record UpdateAdminPublicPageDraftRequest(
+    string Version,
+    string Title,
+    string Subtitle,
+    string SeoTitle,
+    string SeoDescription,
+    bool IsPublished,
+    int SortOrder,
+    IReadOnlyList<PublicPageBlockDto> Blocks);
+
+public sealed record PublishAdminPublicPageRequest(string Version);
+
+public sealed record UpdateAdminPublicContactRequest(
+    string Version,
+    IReadOnlyList<PublicContactChannelDto> ContactPageChannels,
+    IReadOnlyList<PublicContactOfficeDto> ContactPageOffices,
+    IReadOnlyList<PublicContactWorkingHourDto> ContactPageWorkingHours,
+    string ContactPageMapTitle,
+    string ContactPageMapEmbedUrl,
+    bool ContactPageMapIsVisible);
+
 public sealed record PublicPaymentMethodsDto(
     bool CreditCardEnabled,
     bool DebitCardEnabled,
     bool UnpaidRequestEnabled,
     bool PaypalEnabled,
     bool AnyEnabled);
 
 public sealed record PublicSiteSettingsDto(
     string CompanyName,
     string CompanyAddress,
diff --git a/backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs b/backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs
index ebe8052..7497b5c 100644
--- a/backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs
+++ b/backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs
@@ -1,9 +1,14 @@
 using RentACar.API.Contracts.PublicSiteSettings;
 
 namespace RentACar.API.Services;
 
 public interface IPublicSiteSettingsService
 {
     Task<PublicSiteSettingsDto> GetAsync(CancellationToken cancellationToken = default);
     Task<PublicSiteSettingsDto> UpdateAsync(UpdatePublicSiteSettingsRequest request, CancellationToken cancellationToken = default);
+    Task<AdminPublicContentDto> GetAdminContentAsync(CancellationToken cancellationToken = default);
+    Task<AdminPublicContentDto> UpdatePageDraftAsync(string slug, string locale, UpdateAdminPublicPageDraftRequest request, CancellationToken cancellationToken = default);
+    Task<AdminPublicContentDto> PublishPageAsync(string slug, string locale, PublishAdminPublicPageRequest request, CancellationToken cancellationToken = default);
+    Task<AdminPublicContentDto> UnpublishPageAsync(string slug, string locale, PublishAdminPublicPageRequest request, CancellationToken cancellationToken = default);
+    Task<AdminPublicContentDto> UpdateContactContentAsync(UpdateAdminPublicContactRequest request, CancellationToken cancellationToken = default);
 }
diff --git a/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs b/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
index 709005b..3dd1b5f 100644
--- a/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
+++ b/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
@@ -1,10 +1,11 @@
+using System.Globalization;
 using System.Text.Json;
 using System.Text.RegularExpressions;
 using Microsoft.EntityFrameworkCore;
 using RentACar.API.Contracts.PublicSiteSettings;
 using RentACar.Core.Entities;
 using RentACar.Core.Interfaces;
 
 namespace RentACar.API.Services;
 
 public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) : IPublicSiteSettingsService
@@ -78,20 +79,55 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
         settings.ContactPageMapIsVisible = normalized.ContactPageMapIsVisible;
         settings.PagesJson = SerializePages(normalized.Pages);
         settings.UpdatedAt = DateTime.UtcNow;
 
         await dbContext.SaveChangesAsync(cancellationToken);
         var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(dbContext, cancellationToken);
 
         return Map(settings, paymentMethods);
     }
 
+    public async Task<AdminPublicContentDto> GetAdminContentAsync(CancellationToken cancellationToken = default)
+    {
+        var settings = await GetOrCreateAsync(cancellationToken);
+        var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(dbContext, cancellationToken);
+        var dto = Map(settings, paymentMethods);
+
+        return ToAdminContentDto(settings, dto);
+    }
+
+    public Task<AdminPublicContentDto> UpdatePageDraftAsync(
+        string slug,
+        string locale,
+        UpdateAdminPublicPageDraftRequest request,
+        CancellationToken cancellationToken = default) =>
+        throw new NotSupportedException("Task 2 draft behavior is not implemented yet.");
+
+    public Task<AdminPublicContentDto> PublishPageAsync(
+        string slug,
+        string locale,
+        PublishAdminPublicPageRequest request,
+        CancellationToken cancellationToken = default) =>
+        throw new NotSupportedException("Task 2 publish behavior is not implemented yet.");
+
+    public Task<AdminPublicContentDto> UnpublishPageAsync(
+        string slug,
+        string locale,
+        PublishAdminPublicPageRequest request,
+        CancellationToken cancellationToken = default) =>
+        throw new NotSupportedException("Task 2 unpublish behavior is not implemented yet.");
+
+    public Task<AdminPublicContentDto> UpdateContactContentAsync(
+        UpdateAdminPublicContactRequest request,
+        CancellationToken cancellationToken = default) =>
+        throw new NotSupportedException("Task 2 contact update behavior is not implemented yet.");
+
     private async Task<PublicSiteSettings> GetOrCreateAsync(CancellationToken cancellationToken)
     {
         var settings = await dbContext.PublicSiteSettings
             .FirstOrDefaultAsync(x => x.Key == SingletonKey, cancellationToken);
 
         if (settings is not null)
         {
             if (ShouldSeedNewLinkSections(settings))
             {
                 settings.HeaderLinksJson = SerializeLinks(DefaultHeaderLinks());
@@ -611,20 +647,60 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
         DeserializePages(settings.PagesJson, DefaultPages()).OrderBy(x => x.SortOrder).ToList(),
         new PublicPaymentMethodsDto(
             paymentMethods.OnlinePaymentEnabled && paymentMethods.CreditCardEnabled,
             paymentMethods.OnlinePaymentEnabled && paymentMethods.DebitCardEnabled,
             paymentMethods.UnpaidRequestEnabled,
             false,
             paymentMethods.AnyActionableEnabled),
         paymentMethods.OnlinePaymentEnabled,
         settings.UpdatedAt);
 
+    private static AdminPublicContentDto ToAdminContentDto(PublicSiteSettings settings, PublicSiteSettingsDto dto)
+    {
+        var version = settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture);
+        var pages = dto.Pages
+            .Select(page => new AdminPublicManagedPageDto(
+                page.Id,
+                page.Slug,
+                page.Locale,
+                page.Title,
+                page.Subtitle,
+                page.SeoTitle,
+                page.SeoDescription,
+                page.IsPublished,
+                page.SortOrder,
+                page.Blocks,
+                page.IsPublished
+                    ? new PublicPagePublishedSnapshotDto(
+                        page.Title,
+                        page.Subtitle,
+                        page.SeoTitle,
+                        page.SeoDescription,
+                        page.Blocks,
+                        settings.UpdatedAt)
+                    : null,
+                settings.UpdatedAt,
+                page.IsPublished ? settings.UpdatedAt : null))
+            .ToList();
+
+        return new AdminPublicContentDto(
+            version,
+            settings.UpdatedAt,
+            pages,
+            dto.ContactPageChannels,
+            dto.ContactPageOffices,
+            dto.ContactPageWorkingHours,
+            dto.ContactPageMapTitle,
+            dto.ContactPageMapEmbedUrl,
+            dto.ContactPageMapIsVisible);
+    }
+
     private static IReadOnlyList<PublicSiteLinkDto> DefaultHeaderLinks() =>
     [
         new("home", "Ana Sayfa", "/", true, 0),
         new("vehicles", "Araçlar", "/vehicles", true, 1),
         new("about", "Hakkımızda", "/about", true, 2),
         new("contact", "İletişim", "/contact", true, 3),
         new("login", "Giriş Yap", "/dashboard/login/v2", true, 4),
         new("trackReservation", "Rezervasyon Takip", "/track-reservation", true, 5)
     ];
 
diff --git a/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs b/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
index a348757..21b22f0 100644
--- a/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
+++ b/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
@@ -3,20 +3,32 @@ using Microsoft.EntityFrameworkCore;
 using RentACar.API.Contracts.PublicSiteSettings;
 using RentACar.API.Services;
 using RentACar.Core.Entities;
 using RentACar.Infrastructure.Data;
 using Xunit;
 
 namespace RentACar.Tests.Unit.Services;
 
 public sealed class PublicSiteSettingsServiceTests
 {
+    [Fact]
+    public async Task GetAdminContentAsync_returns_versioned_page_content()
+    {
+        await using var dbContext = CreateDbContext();
+        var service = new PublicSiteSettingsService(dbContext);
+
+        var content = await service.GetAdminContentAsync();
+
+        content.Version.Should().NotBeNullOrWhiteSpace();
+        content.Pages.Should().Contain(page => page.Slug == "privacy" && page.Locale == "tr");
+    }
+
     [Fact]
     public async Task GetAsync_WhenMissing_CreatesDefaultSettings()
     {
         await using var dbContext = CreateDbContext();
         var service = new PublicSiteSettingsService(dbContext);
 
         var settings = await service.GetAsync(CancellationToken.None);
 
         settings.CompanyName.Should().Be("Dvn rent a car");
         settings.HeaderLinks.Should().Contain(x => x.Id == "trackReservation" && x.IsVisible);
```
