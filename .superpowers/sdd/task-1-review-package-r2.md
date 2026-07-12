# Task 1 Review Package R2

Base: f0fb79d4c68f87489545cbc3d76bc5ae8021ca10
Head: 63c5ee55dc0196fef2f24bdef2e5c8f4482afc75

## Commit Log
```
63c5ee5 fix(admin): decouple admin public content read
c770f5a feat(admin): add public content contracts
```

## Diff Stat
```
 .superpowers/sdd/task-1-report.md                  | 75 ++++++++++++++++++++++
 .../PublicSiteSettings/PublicSiteSettingsDtos.cs   | 55 ++++++++++++++++
 .../Services/IPublicSiteSettingsService.cs         |  5 ++
 .../Services/PublicSiteSettingsService.cs          | 75 ++++++++++++++++++++++
 .../Services/PublicSiteSettingsServiceTests.cs     | 12 ++++
 5 files changed, 222 insertions(+)
```

## Diff
```diff
diff --git a/.superpowers/sdd/task-1-report.md b/.superpowers/sdd/task-1-report.md
new file mode 100644
index 0000000..d911a15
--- /dev/null
+++ b/.superpowers/sdd/task-1-report.md
@@ -0,0 +1,75 @@
+Status: DONE_WITH_CONCERNS
+
+Commits:
+- `c770f5a` `feat(admin): add public content contracts`
+
+Changed Files:
+- `backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs`
+- `backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs`
+- `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`
+- `backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs`
+
+Concise Test Summary:
+- Added the required failing-first service test for `GetAdminContentAsync`.
+- Verified the test failed first with compile error because `GetAdminContentAsync` did not exist.
+- Implemented Task 1 contracts and minimal admin content mapping.
+- Re-ran the focused brief test and it passed: `1 passed, 0 failed`.
+- Ran Aikido full scan on all modified Task 1 files: `0 issues`.
+
+Concerns:
+- `UpdatePageDraftAsync`, `PublishPageAsync`, `UnpublishPageAsync`, and `UpdateContactContentAsync` are intentionally temporary stubs that throw `NotSupportedException` until Task 2 is implemented.
+- I did not start Task 2 draft/publish/contact mutation behavior.
+- I did not run the broader backend test suite; only the focused brief test was executed, per task scope.
+
+What I Implemented:
+- Added new backend admin content DTO contracts:
+  - `PublicPagePublishedSnapshotDto`
+  - `AdminPublicManagedPageDto`
+  - `AdminPublicContentDto`
+  - `UpdateAdminPublicPageDraftRequest`
+  - `PublishAdminPublicPageRequest`
+  - `UpdateAdminPublicContactRequest`
+- Extended `IPublicSiteSettingsService` with the five admin content method signatures required by the brief.
+- Implemented `GetAdminContentAsync` in `PublicSiteSettingsService` by:
+  - loading current settings through the existing persistence path,
+  - mapping through the current public DTO flow,
+  - projecting an admin-specific content shape with version derived from `settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture)`.
+- Added `ToAdminContentDto(...)` helper to map existing public page/contact data into the new admin contract shape without changing current public `GetAsync()` behavior.
+- Added the focused unit test `GetAdminContentAsync_returns_versioned_page_content`.
+
+Tests Run / Results:
+1. `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore`
+   - First run result: compile failure, expected by brief.
+   - Error observed: `PublicSiteSettingsService` did not contain `GetAdminContentAsync`.
+2. `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore`
+   - Second run result: PASS.
+3. Aikido `aikido_full_scan`
+   - Result: `0 issues`.
+
+Self-Review Notes:
+- Execution boundary changed: `PublicSiteSettingsService` admin content read contract only.
+- Root issue: admin content DTOs and service signatures were missing, so the required test could not compile.
+- Smallest safe fix: add new contracts and a read-only admin projection that reuses existing settings mapping; keep existing public contracts and persistence behavior untouched.
+- Async/cancellation: `GetAdminContentAsync` propagates the incoming `CancellationToken` through the settings load path; admin reads no longer depend on payment-flag lookup.
+- Backward compatibility: existing `GetAsync` and `UpdateAsync` behavior was not changed.
+- Residual risk: the four new admin write methods are only placeholders in this task and will throw until Task 2 supplies the actual draft/publish/contact mutation path.
+
+## Review Fix: Admin Read Decoupling
+
+- Fixed reviewer finding in `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`.
+- Changed `GetAdminContentAsync` to build `AdminPublicContentDto` directly from `PublicSiteSettings` JSON-backed fields.
+- Removed the admin read path dependency on `PaymentMethodFeatureFlags.GetAvailabilityAsync(...)`.
+- Removed the admin read path dependency on the public `Map(...)` projection.
+- Reused existing deserialize/default helpers for pages, contact channels, offices, and working hours.
+- Left `GetAsync`, `UpdateAsync`, and all Task 2 mutation stubs unchanged.
+
+### Focused Validation
+
+1. `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore`
+   - Result: PASS (`1 passed, 0 failed`)
+2. Aikido `aikido_full_scan` on modified first-party code
+   - Result: `0 issues`
+
+### Remaining Concern
+
+- The focused brief test still validates the success path for admin content retrieval, but there is not yet a dedicated regression test that proves admin reads stay independent from future public payment/projection changes.
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
index 709005b..cdf3544 100644
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
@@ -78,20 +79,53 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
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
+
+        return ToAdminContentDto(settings);
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
@@ -611,20 +645,61 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
         DeserializePages(settings.PagesJson, DefaultPages()).OrderBy(x => x.SortOrder).ToList(),
         new PublicPaymentMethodsDto(
             paymentMethods.OnlinePaymentEnabled && paymentMethods.CreditCardEnabled,
             paymentMethods.OnlinePaymentEnabled && paymentMethods.DebitCardEnabled,
             paymentMethods.UnpaidRequestEnabled,
             false,
             paymentMethods.AnyActionableEnabled),
         paymentMethods.OnlinePaymentEnabled,
         settings.UpdatedAt);
 
+    private static AdminPublicContentDto ToAdminContentDto(PublicSiteSettings settings)
+    {
+        var version = settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture);
+        var pages = DeserializePages(settings.PagesJson, DefaultPages())
+            .OrderBy(x => x.SortOrder)
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
+            DeserializeContactChannels(settings.ContactPageChannelsJson, DefaultContactPageChannels()).OrderBy(x => x.SortOrder).ToList(),
+            DeserializeContactOffices(settings.ContactPageOfficesJson, DefaultContactPageOffices()).OrderBy(x => x.SortOrder).ToList(),
+            DeserializeContactWorkingHours(settings.ContactPageWorkingHoursJson, DefaultContactPageWorkingHours()).OrderBy(x => x.SortOrder).ToList(),
+            settings.ContactPageMapTitle,
+            settings.ContactPageMapEmbedUrl,
+            settings.ContactPageMapIsVisible);
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
