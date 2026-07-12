### Task 2: Backend Draft, Publish, and Conflict Behavior

**Files:**
- Modify: `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs`

**Interfaces:**
- Consumes: DTOs and service methods from Task 1.
- Produces: working draft save, publish, unpublish, contact save, and version conflict behavior.

- [ ] **Step 1: Add draft isolation test**

Add:

```csharp
[Fact]
public async Task UpdatePageDraftAsync_does_not_change_public_page_until_publish()
{
    await using var dbContext = CreateDbContext();
    var service = new PublicSiteSettingsService(dbContext);
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
```

- [ ] **Step 2: Add publish test**

Add:

```csharp
[Fact]
public async Task PublishPageAsync_promotes_draft_to_public_page()
{
    await using var dbContext = CreateDbContext();
    var service = new PublicSiteSettingsService(dbContext);
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
```

- [ ] **Step 3: Add conflict test**

Add:

```csharp
[Fact]
public async Task UpdatePageDraftAsync_rejects_stale_version()
{
    await using var dbContext = CreateDbContext();
    var service = new PublicSiteSettingsService(dbContext);

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
```

- [ ] **Step 4: Run tests to verify they fail**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter "UpdatePageDraftAsync|PublishPageAsync" --no-restore
```

Expected: FAIL because draft/publish logic is not implemented.

- [ ] **Step 5: Implement page storage merge**

Store admin draft metadata inside `PagesJson` using the expanded `AdminPublicManagedPageDto` shape. Public `GetAsync()` must map only the `Published` snapshot when present. Legacy pages without `Published` map as published snapshots if `IsPublished` is true.

Implement these helper behaviors in `PublicSiteSettingsService`:

```csharp
private static void EnsureVersion(PublicSiteSettings settings, string version)
{
    var current = settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture);
    if (!string.Equals(current, version, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Public content was updated by another session. Reload before saving.");
    }
}

private static bool IsBuiltInPage(string slug) =>
    string.Equals(slug, "about", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(slug, "terms", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(slug, "privacy", StringComparison.OrdinalIgnoreCase);
```

`UpdatePageDraftAsync` must:

1. Load settings.
2. Check version.
3. Normalize slug and locale.
4. Find existing page by slug+locale or create a new draft.
5. Normalize title, subtitle, SEO, blocks, body format.
6. Preserve existing `Published`.
7. Save settings.
8. Return `GetAdminContentAsync()`.

`PublishPageAsync` must copy current draft fields into `Published`, set `IsPublished = true`, and save.

`UnpublishPageAsync` must clear public visibility by setting `IsPublished = false`; built-in page draft stays present.

- [ ] **Step 6: Run backend content service tests**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter PublicSiteSettingsServiceTests --no-restore
```

Expected: all `PublicSiteSettingsServiceTests` PASS.

- [ ] **Step 7: Commit**

```powershell
git add backend/src/RentACar.API/Services/PublicSiteSettingsService.cs backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs
git commit -m "feat(admin): add draft publish public content behavior"
```

---

