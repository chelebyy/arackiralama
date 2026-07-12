### Task 1: Backend Admin Content Contracts

**Files:**
- Modify: `backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs`
- Modify: `backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs`

**Interfaces:**
- Consumes: existing `PublicSiteSettingsDto`, `PublicManagedPageDto`, `PublicPageBlockDto`.
- Produces: `AdminPublicContentDto`, `AdminPublicManagedPageDto`, `UpdateAdminPublicPageDraftRequest`, `PublishAdminPublicPageRequest`, `UpdateAdminPublicContactRequest`, and service method signatures listed above.

- [ ] **Step 1: Add failing DTO/service compile test**

Add this test to `PublicSiteSettingsServiceTests`:

```csharp
[Fact]
public async Task GetAdminContentAsync_returns_versioned_page_content()
{
    await using var dbContext = CreateDbContext();
    var service = new PublicSiteSettingsService(dbContext);

    var content = await service.GetAdminContentAsync();

    content.Version.Should().NotBeNullOrWhiteSpace();
    content.Pages.Should().Contain(page => page.Slug == "privacy" && page.Locale == "tr");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore
```

Expected: compile failure because `GetAdminContentAsync` and admin content DTOs do not exist.

- [ ] **Step 3: Add DTOs and service signatures**

Update `PublicSiteSettingsDtos.cs` with the DTOs from the Interfaces section. Update `IPublicSiteSettingsService.cs` with the five admin content methods from the Interfaces section.

- [ ] **Step 4: Add temporary minimal service implementation**

In `PublicSiteSettingsService`, add method stubs that compile and delegate to current settings mapping:

```csharp
public async Task<AdminPublicContentDto> GetAdminContentAsync(CancellationToken cancellationToken = default)
{
    var settings = await GetOrCreateSettingsAsync(cancellationToken);
    var dto = ToDto(settings);
    return ToAdminContentDto(settings, dto);
}
```

Add `ToAdminContentDto` as a private helper:

```csharp
private static AdminPublicContentDto ToAdminContentDto(PublicSiteSettings settings, PublicSiteSettingsDto dto)
{
    var version = settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture);
    var pages = dto.Pages
        .Select(page => new AdminPublicManagedPageDto(
            page.Id,
            page.Slug,
            page.Locale,
            page.Title,
            page.Subtitle,
            page.SeoTitle,
            page.SeoDescription,
            page.IsPublished,
            page.SortOrder,
            page.Blocks,
            page.IsPublished
                ? new PublicPagePublishedSnapshotDto(
                    page.Title,
                    page.Subtitle,
                    page.SeoTitle,
                    page.SeoDescription,
                    page.Blocks,
                    settings.UpdatedAt)
                : null,
            settings.UpdatedAt,
            page.IsPublished ? settings.UpdatedAt : null))
        .ToList();

    return new AdminPublicContentDto(
        version,
        settings.UpdatedAt,
        pages,
        dto.ContactPageChannels,
        dto.ContactPageOffices,
        dto.ContactPageWorkingHours,
        dto.ContactPageMapTitle,
        dto.ContactPageMapEmbedUrl,
        dto.ContactPageMapIsVisible);
}
```

Also add the missing using:

```csharp
using System.Globalization;
```

- [ ] **Step 5: Run test to verify it passes**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs backend/src/RentACar.API/Services/PublicSiteSettingsService.cs backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
git commit -m "feat(admin): add public content contracts"
```

---

