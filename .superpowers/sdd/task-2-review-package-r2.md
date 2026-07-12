# Task 2 Review Package R2

Base: 63c5ee55dc0196fef2f24bdef2e5c8f4482afc75
Head: 160e00893d54947c9c95ad27950e183df6fd68ed

## Commit Log
```
160e008 fix(admin): preserve public content snapshots
2233511 feat(admin): add draft publish public content behavior
```

## Diff Stat
```
 .../PublicSiteSettings/PublicSiteSettingsDtos.cs   |   3 +-
 .../Services/PublicSiteSettingsService.cs          | 450 ++++++++++++++++++---
 .../Services/PublicSiteSettingsServiceTests.cs     | 290 +++++++++++++
 3 files changed, 676 insertions(+), 67 deletions(-)
```

## Diff
```diff
diff --git a/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs b/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
index 2149ceb..f2df10c 100644
--- a/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
+++ b/backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
@@ -52,21 +52,22 @@ public sealed record PublicContactWorkingHourDto(
     string Hours,
     bool IsVisible,
     int SortOrder,
     IReadOnlyDictionary<string, PublicLocalizedTextDto>? Translations = null);
 
 public sealed record PublicPageBlockDto(
     string Id,
     string Heading,
     string Body,
     bool IsVisible,
-    int SortOrder);
+    int SortOrder,
+    string BodyFormat = "plain");
 
 public sealed record PublicManagedPageDto(
     string Id,
     string Slug,
     string Locale,
     string Title,
     string Subtitle,
     string SeoTitle,
     string SeoDescription,
     bool IsPublished,
diff --git a/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs b/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
index cdf3544..fe0b53a 100644
--- a/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
+++ b/backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
@@ -53,78 +53,187 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
         var settings = await GetOrCreateAsync(cancellationToken);
         var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(dbContext, cancellationToken);
 
         return Map(settings, paymentMethods);
     }
 
     public async Task<PublicSiteSettingsDto> UpdateAsync(UpdatePublicSiteSettingsRequest request, CancellationToken cancellationToken = default)
     {
         var normalized = Normalize(request);
         var settings = await GetOrCreateAsync(cancellationToken);
+        var updatedAt = DateTime.UtcNow;
 
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
-        settings.PagesJson = SerializePages(normalized.Pages);
-        settings.UpdatedAt = DateTime.UtcNow;
+        settings.PagesJson = SerializeStoredPages(MergeStoredPagesFromPublicRequest(
+            normalized.Pages,
+            DeserializeStoredPages(settings.PagesJson, DefaultStoredPages()),
+            updatedAt));
+        settings.UpdatedAt = updatedAt;
 
         await dbContext.SaveChangesAsync(cancellationToken);
         var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(dbContext, cancellationToken);
 
         return Map(settings, paymentMethods);
     }
 
     public async Task<AdminPublicContentDto> GetAdminContentAsync(CancellationToken cancellationToken = default)
     {
         var settings = await GetOrCreateAsync(cancellationToken);
 
         return ToAdminContentDto(settings);
     }
 
-    public Task<AdminPublicContentDto> UpdatePageDraftAsync(
+    public async Task<AdminPublicContentDto> UpdatePageDraftAsync(
         string slug,
         string locale,
         UpdateAdminPublicPageDraftRequest request,
-        CancellationToken cancellationToken = default) =>
-        throw new NotSupportedException("Task 2 draft behavior is not implemented yet.");
+        CancellationToken cancellationToken = default)
+    {
+        var settings = await GetOrCreateAsync(cancellationToken);
+        EnsureVersion(settings, request.Version);
+
+        var normalizedSlug = NormalizeSlug(slug);
+        var normalizedLocale = NormalizeLocale(locale);
+        var updatedAt = DateTime.UtcNow;
+        var pages = DeserializeStoredPages(settings.PagesJson, DefaultStoredPages())
+            .OrderBy(page => page.SortOrder)
+            .ToList();
+
+        var existing = pages.FirstOrDefault(page =>
+            string.Equals(page.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase) &&
+            string.Equals(page.Locale, normalizedLocale, StringComparison.OrdinalIgnoreCase));
+
+        var published = existing is null ? null : GetPublishedSnapshot(existing);
+        var draft = new StoredPublicManagedPageDto
+        {
+            Id = NormalizeId(existing?.Id ?? string.Empty, $"{normalizedLocale}-{normalizedSlug}"),
+            Slug = normalizedSlug,
+            Locale = normalizedLocale,
+            Title = NormalizeText(request.Title, 160, "Sayfa"),
+            Subtitle = NormalizeText(request.Subtitle, 300, string.Empty),
+            SeoTitle = NormalizeText(request.SeoTitle, 160, request.Title),
+            SeoDescription = NormalizeText(request.SeoDescription, 300, request.Subtitle),
+            IsPublished = existing?.IsPublished ?? false,
+            SortOrder = Math.Max(0, request.SortOrder),
+            Blocks = NormalizePageBlocks(request.Blocks, maxCount: 24),
+            Published = published,
+            DraftUpdatedAtUtc = updatedAt,
+            PublishedAtUtc = existing?.PublishedAtUtc ?? published?.PublishedAtUtc
+        };
+
+        if (existing is null)
+        {
+            pages.Add(draft);
+        }
+        else
+        {
+            var index = pages.IndexOf(existing);
+            pages[index] = draft;
+        }
+
+        settings.PagesJson = SerializeStoredPages(pages);
+        settings.UpdatedAt = updatedAt;
+
+        await dbContext.SaveChangesAsync(cancellationToken);
+        return ToAdminContentDto(settings);
+    }
 
-    public Task<AdminPublicContentDto> PublishPageAsync(
+    public async Task<AdminPublicContentDto> PublishPageAsync(
         string slug,
         string locale,
         PublishAdminPublicPageRequest request,
-        CancellationToken cancellationToken = default) =>
-        throw new NotSupportedException("Task 2 publish behavior is not implemented yet.");
+        CancellationToken cancellationToken = default)
+    {
+        var settings = await GetOrCreateAsync(cancellationToken);
+        EnsureVersion(settings, request.Version);
+
+        var pages = DeserializeStoredPages(settings.PagesJson, DefaultStoredPages()).ToList();
+        var page = GetStoredPage(pages, slug, locale);
+        var updatedAt = DateTime.UtcNow;
+
+        page.Published = new PublicPagePublishedSnapshotDto(
+            page.Title,
+            page.Subtitle,
+            page.SeoTitle,
+            page.SeoDescription,
+            page.Blocks,
+            updatedAt);
+        page.IsPublished = true;
+        page.PublishedAtUtc = updatedAt;
+        page.DraftUpdatedAtUtc ??= updatedAt;
+        settings.PagesJson = SerializeStoredPages(pages);
+        settings.UpdatedAt = updatedAt;
 
-    public Task<AdminPublicContentDto> UnpublishPageAsync(
+        await dbContext.SaveChangesAsync(cancellationToken);
+        return ToAdminContentDto(settings);
+    }
+
+    public async Task<AdminPublicContentDto> UnpublishPageAsync(
         string slug,
         string locale,
         PublishAdminPublicPageRequest request,
-        CancellationToken cancellationToken = default) =>
-        throw new NotSupportedException("Task 2 unpublish behavior is not implemented yet.");
+        CancellationToken cancellationToken = default)
+    {
+        var settings = await GetOrCreateAsync(cancellationToken);
+        EnsureVersion(settings, request.Version);
+
+        var pages = DeserializeStoredPages(settings.PagesJson, DefaultStoredPages()).ToList();
+        var page = GetStoredPage(pages, slug, locale);
+        var updatedAt = DateTime.UtcNow;
 
-    public Task<AdminPublicContentDto> UpdateContactContentAsync(
+        page.IsPublished = false;
+        if (IsBuiltInPage(page.Slug) && page.DraftUpdatedAtUtc is null)
+        {
+            page.DraftUpdatedAtUtc = updatedAt;
+        }
+
+        settings.PagesJson = SerializeStoredPages(pages);
+        settings.UpdatedAt = updatedAt;
+
+        await dbContext.SaveChangesAsync(cancellationToken);
+        return ToAdminContentDto(settings);
+    }
+
+    public async Task<AdminPublicContentDto> UpdateContactContentAsync(
         UpdateAdminPublicContactRequest request,
-        CancellationToken cancellationToken = default) =>
-        throw new NotSupportedException("Task 2 contact update behavior is not implemented yet.");
+        CancellationToken cancellationToken = default)
+    {
+        var settings = await GetOrCreateAsync(cancellationToken);
+        EnsureVersion(settings, request.Version);
+        var updatedAt = DateTime.UtcNow;
+
+        settings.ContactPageChannelsJson = SerializeContactChannels(NormalizeContactChannels(request.ContactPageChannels, maxCount: 8));
+        settings.ContactPageOfficesJson = SerializeContactOffices(NormalizeContactOffices(request.ContactPageOffices, maxCount: 8));
+        settings.ContactPageWorkingHoursJson = SerializeContactWorkingHours(NormalizeContactWorkingHours(request.ContactPageWorkingHours, maxCount: 8));
+        settings.ContactPageMapTitle = NormalizeText(request.ContactPageMapTitle, 160, "Office Locations Map");
+        settings.ContactPageMapEmbedUrl = NormalizeMapEmbedUrl(request.ContactPageMapEmbedUrl);
+        settings.ContactPageMapIsVisible = request.ContactPageMapIsVisible;
+        settings.UpdatedAt = updatedAt;
+
+        await dbContext.SaveChangesAsync(cancellationToken);
+        return ToAdminContentDto(settings);
+    }
 
     private async Task<PublicSiteSettings> GetOrCreateAsync(CancellationToken cancellationToken)
     {
         var settings = await dbContext.PublicSiteSettings
             .FirstOrDefaultAsync(x => x.Key == SingletonKey, cancellationToken);
 
         if (settings is not null)
         {
             if (ShouldSeedNewLinkSections(settings))
             {
@@ -141,63 +250,68 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
                 settings.ContactPageWorkingHoursJson = SerializeContactWorkingHours(DefaultContactPageWorkingHours());
                 settings.ContactPageMapTitle = "Office Locations Map";
                 settings.ContactPageMapEmbedUrl = DefaultContactPageMapEmbedUrl();
                 settings.ContactPageMapIsVisible = true;
                 settings.UpdatedAt = DateTime.UtcNow;
                 await dbContext.SaveChangesAsync(cancellationToken);
             }
 
             if (ShouldSeedPages(settings))
             {
-                settings.PagesJson = SerializePages(DefaultPages());
+                settings.PagesJson = SerializePages(DefaultPages(), DateTime.UtcNow);
                 settings.UpdatedAt = DateTime.UtcNow;
                 await dbContext.SaveChangesAsync(cancellationToken);
             }
 
             if (ApplyBrandDefaults(settings))
             {
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
 
-    private static PublicSiteSettings CreateDefaultSettings() => new()
-    {
-        Key = SingletonKey,
-        CompanyName = DefaultCompanyName,
-        CompanyAddress = "Alanya, Antalya, Türkiye",
-        CompanyPhone = "+90 555 555 01 00",
-        CompanyEmail = "contact@alanyacarrental.com",
-        WorkingHours = "08:00 - 22:00",
-        HeaderLinksJson = SerializeLinks(DefaultHeaderLinks()),
-        HeroLinksJson = SerializeLinks(DefaultHeroLinks()),
-        QuickLinksJson = SerializeLinks(DefaultQuickLinks()),
-        SocialLinksJson = SerializeSocialLinks(DefaultSocialLinks()),
-        FooterBottomLinksJson = SerializeLinks(DefaultFooterBottomLinks()),
-        ContactPageChannelsJson = SerializeContactChannels(DefaultContactPageChannels()),
-        ContactPageOfficesJson = SerializeContactOffices(DefaultContactPageOffices()),
-        ContactPageWorkingHoursJson = SerializeContactWorkingHours(DefaultContactPageWorkingHours()),
-        ContactPageMapTitle = "Office Locations Map",
-        ContactPageMapEmbedUrl = DefaultContactPageMapEmbedUrl(),
-        ContactPageMapIsVisible = true,
-        PagesJson = SerializePages(DefaultPages()),
-        CreatedAt = DateTime.UtcNow,
-        UpdatedAt = DateTime.UtcNow
-    };
+    private static PublicSiteSettings CreateDefaultSettings()
+    {
+        var now = DateTime.UtcNow;
+
+        return new PublicSiteSettings
+        {
+            Key = SingletonKey,
+            CompanyName = DefaultCompanyName,
+            CompanyAddress = "Alanya, Antalya, Türkiye",
+            CompanyPhone = "+90 555 555 01 00",
+            CompanyEmail = "contact@alanyacarrental.com",
+            WorkingHours = "08:00 - 22:00",
+            HeaderLinksJson = SerializeLinks(DefaultHeaderLinks()),
+            HeroLinksJson = SerializeLinks(DefaultHeroLinks()),
+            QuickLinksJson = SerializeLinks(DefaultQuickLinks()),
+            SocialLinksJson = SerializeSocialLinks(DefaultSocialLinks()),
+            FooterBottomLinksJson = SerializeLinks(DefaultFooterBottomLinks()),
+            ContactPageChannelsJson = SerializeContactChannels(DefaultContactPageChannels()),
+            ContactPageOfficesJson = SerializeContactOffices(DefaultContactPageOffices()),
+            ContactPageWorkingHoursJson = SerializeContactWorkingHours(DefaultContactPageWorkingHours()),
+            ContactPageMapTitle = "Office Locations Map",
+            ContactPageMapEmbedUrl = DefaultContactPageMapEmbedUrl(),
+            ContactPageMapIsVisible = true,
+            PagesJson = SerializePages(DefaultPages(), now),
+            CreatedAt = now,
+            UpdatedAt = now
+        };
+    }
 
     private static bool ApplyBrandDefaults(PublicSiteSettings settings)
     {
         var changed = false;
 
         if (settings.CompanyName == LegacyCompanyName)
         {
             settings.CompanyName = DefaultCompanyName;
             changed = true;
         }
@@ -389,21 +503,22 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
 
     private static IReadOnlyList<PublicPageBlockDto> NormalizePageBlocks(IReadOnlyList<PublicPageBlockDto>? blocks, int maxCount)
     {
         return (blocks ?? [])
             .Take(maxCount)
             .Select((block, index) => block with
             {
                 Id = NormalizeId(block.Id, $"block-{index + 1}"),
                 Heading = NormalizeText(block.Heading, 160, "Bölüm"),
                 Body = NormalizeText(block.Body, 5000, string.Empty),
-                SortOrder = index
+                SortOrder = index,
+                BodyFormat = NormalizeBodyFormat(block.BodyFormat)
             })
             .ToList();
     }
 
     private static string NormalizeSlug(string slug)
     {
         var value = NormalizeText(slug, 80, "sayfa").ToLowerInvariant();
         value = Regex.Replace(value, @"[^a-z0-9-]", "-");
         value = Regex.Replace(value, "-{2,}", "-").Trim('-');
         if (string.IsNullOrWhiteSpace(value) || !SafeSlugRegex.IsMatch(value))
@@ -467,20 +582,37 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
             uri.Scheme != Uri.UriSchemeHttps ||
             !IsGoogleHost(uri.Host) ||
             !uri.AbsolutePath.StartsWith("/maps/embed", StringComparison.OrdinalIgnoreCase))
         {
             throw new ArgumentException("Harita bağlantısı geçerli bir Google Maps embed URL'i olmalıdır.");
         }
 
         return uri.ToString();
     }
 
+    private static string NormalizeLocale(string locale)
+    {
+        var normalized = NormalizeText(locale, 12, "tr").ToLowerInvariant();
+        if (!SupportedPublicLocales.Contains(normalized))
+        {
+            throw new ArgumentException($"Desteklenmeyen public site dili: {normalized}");
+        }
+
+        return normalized;
+    }
+
+    private static string NormalizeBodyFormat(string bodyFormat)
+    {
+        var normalized = NormalizeText(bodyFormat, 16, "plain").ToLowerInvariant();
+        return normalized is "html" or "plain" ? normalized : "plain";
+    }
+
     private static bool IsGoogleHost(string host) =>
         host.Equals("google.com", StringComparison.OrdinalIgnoreCase) ||
         host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase);
 
     private static string NormalizeId(string id, string fallback)
     {
         var value = string.IsNullOrWhiteSpace(id) ? fallback : id.Trim();
         return Regex.Replace(value, @"[^a-zA-Z0-9_-]", "-")[..Math.Min(value.Length, 60)];
     }
 
@@ -569,21 +701,24 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
 
     private static string SerializeContactChannels(IReadOnlyList<PublicContactChannelDto> channels) =>
         JsonSerializer.Serialize(channels.OrderBy(x => x.SortOrder), JsonOptions);
 
     private static string SerializeContactOffices(IReadOnlyList<PublicContactOfficeDto> offices) =>
         JsonSerializer.Serialize(offices.OrderBy(x => x.SortOrder), JsonOptions);
 
     private static string SerializeContactWorkingHours(IReadOnlyList<PublicContactWorkingHourDto> rows) =>
         JsonSerializer.Serialize(rows.OrderBy(x => x.SortOrder), JsonOptions);
 
-    private static string SerializePages(IReadOnlyList<PublicManagedPageDto> pages) =>
+    private static string SerializePages(IReadOnlyList<PublicManagedPageDto> pages, DateTime updatedAt) =>
+        SerializeStoredPages(ToStoredPages(pages, updatedAt));
+
+    private static string SerializeStoredPages(IReadOnlyList<StoredPublicManagedPageDto> pages) =>
         JsonSerializer.Serialize(pages.OrderBy(x => x.SortOrder), JsonOptions);
 
     private static IReadOnlyList<PublicSiteLinkDto> DeserializeLinks(string json, IReadOnlyList<PublicSiteLinkDto> fallback)
     {
         return JsonSerializer.Deserialize<IReadOnlyList<PublicSiteLinkDto>>(json, JsonOptions) ?? fallback;
     }
 
     private static IReadOnlyList<PublicSocialLinkDto> DeserializeSocialLinks(string json, IReadOnlyList<PublicSocialLinkDto> fallback)
     {
         return JsonSerializer.Deserialize<IReadOnlyList<PublicSocialLinkDto>>(json, JsonOptions) ?? fallback;
@@ -597,23 +732,24 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
     private static IReadOnlyList<PublicContactOfficeDto> DeserializeContactOffices(string json, IReadOnlyList<PublicContactOfficeDto> fallback)
     {
         return JsonSerializer.Deserialize<IReadOnlyList<PublicContactOfficeDto>>(json, JsonOptions) ?? fallback;
     }
 
     private static IReadOnlyList<PublicContactWorkingHourDto> DeserializeContactWorkingHours(string json, IReadOnlyList<PublicContactWorkingHourDto> fallback)
     {
         return JsonSerializer.Deserialize<IReadOnlyList<PublicContactWorkingHourDto>>(json, JsonOptions) ?? fallback;
     }
 
-    private static IReadOnlyList<PublicManagedPageDto> DeserializePages(string json, IReadOnlyList<PublicManagedPageDto> fallback)
+    private static IReadOnlyList<StoredPublicManagedPageDto> DeserializeStoredPages(string json, IReadOnlyList<StoredPublicManagedPageDto> fallback)
     {
-        return JsonSerializer.Deserialize<IReadOnlyList<PublicManagedPageDto>>(json, JsonOptions) ?? fallback;
+        var pages = JsonSerializer.Deserialize<List<StoredPublicManagedPageDto>>(json, JsonOptions);
+        return pages is { Count: > 0 } ? pages : fallback;
     }
 
     private static bool ShouldSeedNewLinkSections(PublicSiteSettings settings) =>
         IsEmptyJsonArray(settings.HeaderLinksJson) &&
         IsEmptyJsonArray(settings.HeroLinksJson) &&
         settings.CreatedAt == settings.UpdatedAt;
 
     private static bool ShouldSeedContactPageSections(PublicSiteSettings settings) =>
         IsEmptyJsonArray(settings.ContactPageChannelsJson) &&
         IsEmptyJsonArray(settings.ContactPageOfficesJson) &&
@@ -635,71 +771,236 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
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
-        DeserializePages(settings.PagesJson, DefaultPages()).OrderBy(x => x.SortOrder).ToList(),
+        DeserializeStoredPages(settings.PagesJson, DefaultStoredPages())
+            .OrderBy(x => x.SortOrder)
+            .Select(ToPublicPageDto)
+            .ToList(),
         new PublicPaymentMethodsDto(
             paymentMethods.OnlinePaymentEnabled && paymentMethods.CreditCardEnabled,
             paymentMethods.OnlinePaymentEnabled && paymentMethods.DebitCardEnabled,
             paymentMethods.UnpaidRequestEnabled,
             false,
             paymentMethods.AnyActionableEnabled),
         paymentMethods.OnlinePaymentEnabled,
         settings.UpdatedAt);
 
     private static AdminPublicContentDto ToAdminContentDto(PublicSiteSettings settings)
     {
         var version = settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture);
-        var pages = DeserializePages(settings.PagesJson, DefaultPages())
+        var pages = DeserializeStoredPages(settings.PagesJson, DefaultStoredPages())
             .OrderBy(x => x.SortOrder)
-            .Select(page => new AdminPublicManagedPageDto(
-                page.Id,
-                page.Slug,
-                page.Locale,
-                page.Title,
-                page.Subtitle,
-                page.SeoTitle,
-                page.SeoDescription,
-                page.IsPublished,
-                page.SortOrder,
-                page.Blocks,
-                page.IsPublished
-                    ? new PublicPagePublishedSnapshotDto(
-                        page.Title,
-                        page.Subtitle,
-                        page.SeoTitle,
-                        page.SeoDescription,
-                        page.Blocks,
-                        settings.UpdatedAt)
-                    : null,
-                settings.UpdatedAt,
-                page.IsPublished ? settings.UpdatedAt : null))
+            .Select(page => ToAdminPageDto(page, settings.UpdatedAt))
             .ToList();
 
         return new AdminPublicContentDto(
             version,
             settings.UpdatedAt,
             pages,
             DeserializeContactChannels(settings.ContactPageChannelsJson, DefaultContactPageChannels()).OrderBy(x => x.SortOrder).ToList(),
             DeserializeContactOffices(settings.ContactPageOfficesJson, DefaultContactPageOffices()).OrderBy(x => x.SortOrder).ToList(),
             DeserializeContactWorkingHours(settings.ContactPageWorkingHoursJson, DefaultContactPageWorkingHours()).OrderBy(x => x.SortOrder).ToList(),
             settings.ContactPageMapTitle,
             settings.ContactPageMapEmbedUrl,
             settings.ContactPageMapIsVisible);
     }
 
+    private static IReadOnlyList<StoredPublicManagedPageDto> ToStoredPages(IReadOnlyList<PublicManagedPageDto> pages, DateTime updatedAt)
+    {
+        return pages
+            .OrderBy(page => page.SortOrder)
+            .Select(page => new StoredPublicManagedPageDto
+            {
+                Id = page.Id,
+                Slug = page.Slug,
+                Locale = page.Locale,
+                Title = page.Title,
+                Subtitle = page.Subtitle,
+                SeoTitle = page.SeoTitle,
+                SeoDescription = page.SeoDescription,
+                IsPublished = page.IsPublished,
+                SortOrder = page.SortOrder,
+                Blocks = page.Blocks,
+                Published = page.IsPublished
+                    ? new PublicPagePublishedSnapshotDto(
+                        page.Title,
+                        page.Subtitle,
+                        page.SeoTitle,
+                        page.SeoDescription,
+                        page.Blocks,
+                        updatedAt)
+                    : null,
+                DraftUpdatedAtUtc = updatedAt,
+                PublishedAtUtc = page.IsPublished ? updatedAt : null
+            })
+            .ToList();
+    }
+
+    private static IReadOnlyList<StoredPublicManagedPageDto> MergeStoredPagesFromPublicRequest(
+        IReadOnlyList<PublicManagedPageDto> pages,
+        IReadOnlyList<StoredPublicManagedPageDto> existingPages,
+        DateTime updatedAt)
+    {
+        var existingByKey = existingPages
+            .GroupBy(page => PageKey(page.Locale, page.Slug), StringComparer.OrdinalIgnoreCase)
+            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
+
+        return pages
+            .OrderBy(page => page.SortOrder)
+            .Select(page =>
+            {
+                existingByKey.TryGetValue(PageKey(page.Locale, page.Slug), out var existing);
+                var published = existing is null
+                    ? CreatePublishedSnapshot(page, updatedAt)
+                    : GetPublishedSnapshot(existing);
+
+                if (existing is not null && published is null && page.IsPublished)
+                {
+                    published = CreatePublishedSnapshot(page, updatedAt);
+                }
+
+                return new StoredPublicManagedPageDto
+                {
+                    Id = page.Id,
+                    Slug = page.Slug,
+                    Locale = page.Locale,
+                    Title = page.Title,
+                    Subtitle = page.Subtitle,
+                    SeoTitle = page.SeoTitle,
+                    SeoDescription = page.SeoDescription,
+                    IsPublished = page.IsPublished,
+                    SortOrder = page.SortOrder,
+                    Blocks = page.Blocks,
+                    Published = published,
+                    DraftUpdatedAtUtc = existing?.DraftUpdatedAtUtc ?? updatedAt,
+                    PublishedAtUtc = existing?.PublishedAtUtc ?? published?.PublishedAtUtc
+                };
+            })
+            .ToList();
+    }
+
+    private static PublicPagePublishedSnapshotDto? CreatePublishedSnapshot(PublicManagedPageDto page, DateTime updatedAt)
+    {
+        return page.IsPublished
+            ? new PublicPagePublishedSnapshotDto(
+                page.Title,
+                page.Subtitle,
+                page.SeoTitle,
+                page.SeoDescription,
+                page.Blocks,
+                updatedAt)
+            : null;
+    }
+
+    private static string PageKey(string locale, string slug) =>
+        $"{NormalizeLocale(locale)}:{NormalizeSlug(slug)}";
+
+    private static IReadOnlyList<StoredPublicManagedPageDto> DefaultStoredPages()
+    {
+        var now = DateTime.UtcNow;
+        return ToStoredPages(DefaultPages(), now);
+    }
+
+    private static PublicManagedPageDto ToPublicPageDto(StoredPublicManagedPageDto page)
+    {
+        var published = GetPublishedSnapshot(page);
+        var title = published?.Title ?? page.Title;
+        var subtitle = published?.Subtitle ?? page.Subtitle;
+        var seoTitle = published?.SeoTitle ?? page.SeoTitle;
+        var seoDescription = published?.SeoDescription ?? page.SeoDescription;
+        var blocks = published?.Blocks ?? page.Blocks;
+
+        return new PublicManagedPageDto(
+            page.Id,
+            page.Slug,
+            page.Locale,
+            title,
+            subtitle,
+            seoTitle,
+            seoDescription,
+            page.IsPublished,
+            page.SortOrder,
+            blocks);
+    }
+
+    private static AdminPublicManagedPageDto ToAdminPageDto(StoredPublicManagedPageDto page, DateTime fallbackTimestamp)
+    {
+        var published = GetPublishedSnapshot(page);
+        return new AdminPublicManagedPageDto(
+            page.Id,
+            page.Slug,
+            page.Locale,
+            page.Title,
+            page.Subtitle,
+            page.SeoTitle,
+            page.SeoDescription,
+            page.IsPublished,
+            page.SortOrder,
+            page.Blocks,
+            published,
+            page.DraftUpdatedAtUtc ?? fallbackTimestamp,
+            published is null ? page.PublishedAtUtc : page.PublishedAtUtc ?? fallbackTimestamp);
+    }
+
+    private static PublicPagePublishedSnapshotDto? GetPublishedSnapshot(StoredPublicManagedPageDto page)
+    {
+        if (page.Published is not null)
+        {
+            return page.Published with
+            {
+                Blocks = NormalizePageBlocks(page.Published.Blocks, maxCount: 24)
+            };
+        }
+
+        return page.IsPublished
+            ? new PublicPagePublishedSnapshotDto(
+                page.Title,
+                page.Subtitle,
+                page.SeoTitle,
+                page.SeoDescription,
+                NormalizePageBlocks(page.Blocks, maxCount: 24),
+                page.PublishedAtUtc ?? page.DraftUpdatedAtUtc ?? DateTime.UtcNow)
+            : null;
+    }
+
+    private static StoredPublicManagedPageDto GetStoredPage(IReadOnlyList<StoredPublicManagedPageDto> pages, string slug, string locale)
+    {
+        var normalizedSlug = NormalizeSlug(slug);
+        var normalizedLocale = NormalizeLocale(locale);
+
+        return pages.FirstOrDefault(page =>
+                   string.Equals(page.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase) &&
+                   string.Equals(page.Locale, normalizedLocale, StringComparison.OrdinalIgnoreCase))
+               ?? throw new InvalidOperationException($"Public page '{normalizedLocale}/{normalizedSlug}' was not found.");
+    }
+
+    private static void EnsureVersion(PublicSiteSettings settings, string version)
+    {
+        var current = settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture);
+        if (!string.Equals(current, version, StringComparison.Ordinal))
+        {
+            throw new InvalidOperationException("Public content was updated by another session. Reload before saving.");
+        }
+    }
+
+    private static bool IsBuiltInPage(string slug) =>
+        string.Equals(slug, "about", StringComparison.OrdinalIgnoreCase) ||
+        string.Equals(slug, "terms", StringComparison.OrdinalIgnoreCase) ||
+        string.Equals(slug, "privacy", StringComparison.OrdinalIgnoreCase);
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
 
@@ -798,11 +1099,28 @@ public sealed class PublicSiteSettingsService(IApplicationDbContext dbContext) :
             true,
             2,
             [
                 new("collection", "Toplanan Bilgiler", "Rezervasyon, iletişim ve ödeme süreçleri için gerekli müşteri ve rezervasyon bilgileri işlenir.", true, 0),
                 new("rights", "Haklarınız", "Kişisel verilerinizle ilgili bilgi alma, düzeltme ve silme taleplerinizi iletişim kanallarımız üzerinden iletebilirsiniz.", true, 1)
             ])
     ];
 
     private static string DefaultContactPageMapEmbedUrl() =>
         "https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d128084.037171682!2d31.95928245!3d36.54115!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x14dca27b8223b0b7%3A0x403b37d0ec0cb80!2sAlanya%2C%20Antalya%2C%20Turkey!5e0!3m2!1sen!2sus!4v1700000000000!5m2!1sen!2sus";
+
+    private sealed class StoredPublicManagedPageDto
+    {
+        public string Id { get; set; } = string.Empty;
+        public string Slug { get; set; } = string.Empty;
+        public string Locale { get; set; } = "tr";
+        public string Title { get; set; } = string.Empty;
+        public string Subtitle { get; set; } = string.Empty;
+        public string SeoTitle { get; set; } = string.Empty;
+        public string SeoDescription { get; set; } = string.Empty;
+        public bool IsPublished { get; set; }
+        public int SortOrder { get; set; }
+        public IReadOnlyList<PublicPageBlockDto> Blocks { get; set; } = [];
+        public PublicPagePublishedSnapshotDto? Published { get; set; }
+        public DateTime? DraftUpdatedAtUtc { get; set; }
+        public DateTime? PublishedAtUtc { get; set; }
+    }
 }
diff --git a/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs b/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
index 21b22f0..2d98581 100644
--- a/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
+++ b/backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
@@ -1,10 +1,11 @@
+using System.Text.Json;
 using FluentAssertions;
 using Microsoft.EntityFrameworkCore;
 using RentACar.API.Contracts.PublicSiteSettings;
 using RentACar.API.Services;
 using RentACar.Core.Entities;
 using RentACar.Infrastructure.Data;
 using Xunit;
 
 namespace RentACar.Tests.Unit.Services;
 
@@ -15,20 +16,190 @@ public sealed class PublicSiteSettingsServiceTests
     {
         await using var dbContext = CreateDbContext();
         var service = new PublicSiteSettingsService(dbContext);
 
         var content = await service.GetAdminContentAsync();
 
         content.Version.Should().NotBeNullOrWhiteSpace();
         content.Pages.Should().Contain(page => page.Slug == "privacy" && page.Locale == "tr");
     }
 
+    [Fact]
+    public async Task UpdatePageDraftAsync_does_not_change_public_page_until_publish()
+    {
+        await using var dbContext = CreateDbContext();
+        var service = new PublicSiteSettingsService(dbContext);
+        var before = await service.GetAsync();
+        var version = (await service.GetAdminContentAsync()).Version;
+
+        await service.UpdatePageDraftAsync(
+            "privacy",
+            "tr",
+            new UpdateAdminPublicPageDraftRequest(
+                version,
+                "Taslak Gizlilik",
+                "Taslak alt başlık",
+                "Taslak SEO",
+                "Taslak SEO açıklaması",
+                true,
+                0,
+                [
+                    new PublicPageBlockDto("block-1", "Taslak Bölüm", "<p>Taslak içerik</p>", true, 0, "html")
+                ]),
+            CancellationToken.None);
+
+        var publicAfterDraft = await service.GetAsync();
+
+        publicAfterDraft.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr")
+            .Title.Should().Be(before.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr").Title);
+    }
+
+    [Fact]
+    public async Task UpdatePageDraftAsync_WhenLegacyPublishedPageHasNoSnapshot_DoesNotLeakDraft()
+    {
+        await using var dbContext = CreateDbContext();
+        var publishedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
+        var pagesJson = JsonSerializer.Serialize(new[]
+        {
+            new
+            {
+                id = "tr-privacy",
+                slug = "privacy",
+                locale = "tr",
+                title = "Legacy Privacy",
+                subtitle = "Legacy subtitle",
+                seoTitle = "Legacy SEO",
+                seoDescription = "Legacy description",
+                isPublished = true,
+                sortOrder = 0,
+                blocks = new[]
+                {
+                    new { id = "legacy", heading = "Legacy Heading", body = "Legacy body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
+                },
+                published = (object?)null,
+                draftUpdatedAtUtc = (DateTime?)null,
+                publishedAtUtc = (DateTime?)publishedAt
+            }
+        });
+        dbContext.PublicSiteSettings.Add(CreateSettingsWithPages(pagesJson));
+        await dbContext.SaveChangesAsync();
+        var service = new PublicSiteSettingsService(dbContext);
+        var version = (await service.GetAdminContentAsync()).Version;
+
+        var adminAfterDraft = await service.UpdatePageDraftAsync(
+            "privacy",
+            "tr",
+            new UpdateAdminPublicPageDraftRequest(
+                version,
+                "Draft Privacy",
+                "Draft subtitle",
+                "Draft SEO",
+                "Draft description",
+                true,
+                0,
+                [new PublicPageBlockDto("draft", "Draft Heading", "Draft body", true, 0, "plain")]),
+            CancellationToken.None);
+        var publicAfterDraft = await service.GetAsync();
+
+        var publicPage = publicAfterDraft.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
+        publicPage.Title.Should().Be("Legacy Privacy");
+        publicPage.Blocks.Single().Body.Should().Be("Legacy body");
+        var adminPage = adminAfterDraft.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
+        adminPage.Title.Should().Be("Draft Privacy");
+        adminPage.Published.Should().NotBeNull();
+        adminPage.Published!.Title.Should().Be("Legacy Privacy");
+        adminPage.PublishedAtUtc.Should().Be(publishedAt);
+
+        await service.PublishPageAsync("privacy", "tr", new PublishAdminPublicPageRequest(adminAfterDraft.Version), CancellationToken.None);
+        var publicAfterPublish = await service.GetAsync();
+
+        publicAfterPublish.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr")
+            .Title.Should().Be("Draft Privacy");
+    }
+
+    [Fact]
+    public async Task PublishPageAsync_promotes_draft_to_public_page()
+    {
+        await using var dbContext = CreateDbContext();
+        var service = new PublicSiteSettingsService(dbContext);
+        var version = (await service.GetAdminContentAsync()).Version;
+
+        var afterDraft = await service.UpdatePageDraftAsync(
+            "privacy",
+            "tr",
+            new UpdateAdminPublicPageDraftRequest(
+                version,
+                "Yayınlanacak Gizlilik",
+                "Yeni alt başlık",
+                "Yeni SEO",
+                "Yeni SEO açıklaması",
+                true,
+                0,
+                [
+                    new PublicPageBlockDto("block-1", "Yeni Bölüm", "<p>Yeni içerik</p>", true, 0, "html")
+                ]),
+            CancellationToken.None);
+
+        await service.PublishPageAsync("privacy", "tr", new PublishAdminPublicPageRequest(afterDraft.Version), CancellationToken.None);
+
+        var publicAfterPublish = await service.GetAsync();
+        var page = publicAfterPublish.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
+
+        page.Title.Should().Be("Yayınlanacak Gizlilik");
+        page.Blocks.Single().BodyFormat.Should().Be("html");
+    }
+
+    [Fact]
+    public async Task UnpublishPageAsync_preserves_last_published_snapshot()
+    {
+        await using var dbContext = CreateDbContext();
+        var service = new PublicSiteSettingsService(dbContext);
+        var publicBefore = await service.GetAsync();
+        var version = (await service.GetAdminContentAsync()).Version;
+
+        var adminAfterUnpublish = await service.UnpublishPageAsync(
+            "privacy",
+            "tr",
+            new PublishAdminPublicPageRequest(version),
+            CancellationToken.None);
+
+        var page = adminAfterUnpublish.Pages.Single(page => page.Slug == "privacy" && page.Locale == "tr");
+        page.IsPublished.Should().BeFalse();
+        page.Published.Should().NotBeNull();
+        page.Published!.Title.Should().Be(publicBefore.Pages.Single(publicPage => publicPage.Slug == "privacy" && publicPage.Locale == "tr").Title);
+        page.PublishedAtUtc.Should().NotBeNull();
+    }
+
+    [Fact]
+    public async Task UpdatePageDraftAsync_rejects_stale_version()
+    {
+        await using var dbContext = CreateDbContext();
+        var service = new PublicSiteSettingsService(dbContext);
+
+        var act = () => service.UpdatePageDraftAsync(
+            "privacy",
+            "tr",
+            new UpdateAdminPublicPageDraftRequest(
+                "1",
+                "Stale",
+                "",
+                "",
+                "",
+                true,
+                0,
+                [new PublicPageBlockDto("block-1", "Bölüm", "İçerik", true, 0, "plain")]),
+            CancellationToken.None);
+
+        await act.Should().ThrowAsync<InvalidOperationException>()
+            .WithMessage("Public content was updated by another session. Reload before saving.");
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
@@ -240,20 +411,92 @@ public sealed class PublicSiteSettingsServiceTests
         office.Translations!["en"].Address.Should().Be("English address");
         var workingHour = reloaded.ContactPageWorkingHours.Should().ContainSingle(x => x.Day == "Managed Day" && x.SortOrder == 0).Subject;
         workingHour.Translations!["en"].Hours.Should().Be("13:00 - 17:00");
         reloaded.ContactPageMapTitle.Should().Be("Managed Map");
         reloaded.ContactPageMapEmbedUrl.Should().Be("https://www.google.com/maps/embed?pb=managed");
         reloaded.ContactPageMapIsVisible.Should().BeFalse();
         reloaded.Pages.Should().ContainSingle(x => x.Slug == "terms" && x.Locale == "tr" && !x.IsPublished);
         reloaded.Pages.Single().Blocks.Should().ContainSingle(x => x.SortOrder == 0);
     }
 
+    [Fact]
+    public async Task UpdateAsync_PreservesExistingPagePublicationMetadata()
+    {
+        await using var dbContext = CreateDbContext();
+        var draftAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);
+        var publishedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);
+        var pagesJson = JsonSerializer.Serialize(new[]
+        {
+            new
+            {
+                id = "tr-terms",
+                slug = "terms",
+                locale = "tr",
+                title = "Stored Draft Terms",
+                subtitle = "Stored draft subtitle",
+                seoTitle = "Stored draft SEO",
+                seoDescription = "Stored draft description",
+                isPublished = false,
+                sortOrder = 0,
+                blocks = new[]
+                {
+                    new { id = "draft", heading = "Draft Heading", body = "Stored draft body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
+                },
+                published = new
+                {
+                    title = "Published Terms",
+                    subtitle = "Published subtitle",
+                    seoTitle = "Published SEO",
+                    seoDescription = "Published description",
+                    blocks = new[]
+                    {
+                        new { id = "published", heading = "Published Heading", body = "Published body", isVisible = true, sortOrder = 0, bodyFormat = "plain" }
+                    },
+                    publishedAtUtc = publishedAt
+                },
+                draftUpdatedAtUtc = (DateTime?)draftAt,
+                publishedAtUtc = (DateTime?)publishedAt
+            }
+        });
+        dbContext.PublicSiteSettings.Add(CreateSettingsWithPages(pagesJson));
+        await dbContext.SaveChangesAsync();
+        var service = new PublicSiteSettingsService(dbContext);
+        var request = CreateSettingsRequest(
+        [
+            new PublicManagedPageDto(
+                "tr-terms",
+                "terms",
+                "TR",
+                "Settings Draft Terms",
+                "Settings draft subtitle",
+                "Settings draft SEO",
+                "Settings draft description",
+                false,
+                0,
+                [new PublicPageBlockDto("settings-draft", "Settings Draft Heading", "Settings draft body", true, 0)])
+        ]);
+
+        await service.UpdateAsync(request, CancellationToken.None);
+
+        var adminAfterUpdate = await service.GetAdminContentAsync();
+        var adminPage = adminAfterUpdate.Pages.Single(page => page.Slug == "terms" && page.Locale == "tr");
+        adminPage.Title.Should().Be("Settings Draft Terms");
+        adminPage.Published.Should().NotBeNull();
+        adminPage.Published!.Title.Should().Be("Published Terms");
+        adminPage.Published.Blocks.Single().Body.Should().Be("Published body");
+        adminPage.DraftUpdatedAtUtc.Should().Be(draftAt);
+        adminPage.PublishedAtUtc.Should().Be(publishedAt);
+        var publicPage = (await service.GetAsync()).Pages.Single(page => page.Slug == "terms" && page.Locale == "tr");
+        publicPage.Title.Should().Be("Published Terms");
+        publicPage.IsPublished.Should().BeFalse();
+    }
+
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
@@ -349,11 +592,58 @@ public sealed class PublicSiteSettingsServiceTests
     }
 
     private static RentACarDbContext CreateDbContext()
     {
         var options = new DbContextOptionsBuilder<RentACarDbContext>()
             .UseInMemoryDatabase(Guid.NewGuid().ToString())
             .Options;
 
         return new RentACarDbContext(options);
     }
+
+    private static PublicSiteSettings CreateSettingsWithPages(string pagesJson)
+    {
+        var now = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
+        return new PublicSiteSettings
+        {
+            Key = "public-site",
+            CompanyName = "Dvn rent a car",
+            CompanyAddress = "Alanya",
+            CompanyPhone = "+90 555",
+            CompanyEmail = "info@example.test",
+            WorkingHours = "09:00 - 18:00",
+            HeaderLinksJson = "[]",
+            HeroLinksJson = "[]",
+            QuickLinksJson = "[]",
+            SocialLinksJson = "[]",
+            FooterBottomLinksJson = "[]",
+            ContactPageChannelsJson = "[]",
+            ContactPageOfficesJson = "[]",
+            ContactPageWorkingHoursJson = "[]",
+            ContactPageMapTitle = "Map",
+            ContactPageMapEmbedUrl = "https://www.google.com/maps/embed?pb=managed",
+            ContactPageMapIsVisible = true,
+            PagesJson = pagesJson,
+            CreatedAt = now,
+            UpdatedAt = now
+        };
+    }
+
+    private static UpdatePublicSiteSettingsRequest CreateSettingsRequest(IReadOnlyList<PublicManagedPageDto> pages) => new(
+        "Managed Rent",
+        "Alanya",
+        "+90 555",
+        "managed@example.test",
+        "09:00 - 18:00",
+        [],
+        [],
+        [],
+        [],
+        [],
+        [],
+        [],
+        [],
+        "Map",
+        "https://www.google.com/maps/embed?pb=managed",
+        true,
+        pages);
 }
```
