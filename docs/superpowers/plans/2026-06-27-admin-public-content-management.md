# Admin Public Content Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a practical admin content-management workflow for public pages and contact content, with real draft/publish behavior, safer rich-text editing, and public rendering that never leaks drafts.

**Architecture:** Keep the existing `PublicSiteSettings` aggregate as the persistence boundary, but split admin content editing away from the broad system settings screen. Add admin-focused content contracts and service methods for page drafts, publishing, unpublishing, and contact updates, while preserving the current public endpoint shape. The frontend gets a dedicated `/dashboard/settings/public-content` workspace with page/contact tabs, a constrained rich-text editor, and a sanitizer-backed public render path.

**Tech Stack:** .NET 10 / ASP.NET Core, EF Core, PostgreSQL JSON string storage in `PublicSiteSettings`, Next.js 16 App Router, React 19, TypeScript, react-hook-form, zod, SWR, Tiptap, DOMPurify, Vitest, Testing Library, Playwright.

## Global Constraints

- Public frontend must remain corporate-minimal, light-only, desktop-first, and separate from admin design language.
- Admin/dashboard may use shadcn/ui components.
- Public pages must not use shadcn/ui components or shadcn design language.
- Keep existing public API behavior backward-compatible unless a task explicitly defines a new admin-only contract.
- Never expose draft page content from public runtime APIs or public page rendering.
- Use existing locale set exactly: `tr`, `en`, `ru`, `ar`, `de`.
- Built-in slugs are `about`, `terms`, `privacy`; built-in pages may be unpublished but not deleted.
- Do not add media upload, version history, approval workflow, or analytics in this plan.
- Run Aikido full scan on generated, added, and modified first-party code after implementation.
- Use Context7 for library/API documentation checks when library usage changes.

---

## File Structure

- Modify `backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs`: keep current public DTOs and add admin content DTOs for draft/published page state.
- Modify `backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs`: add draft/publish/contact methods consumed by the admin controller.
- Modify `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`: add legacy page normalization, draft/publish merge logic, contact-only updates, and optimistic concurrency checks.
- Create `backend/src/RentACar.API/Controllers/AdminPublicContentController.cs`: expose admin-only content endpoints under `/api/admin/v1/public-content`.
- Modify `backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs`: cover legacy migration, draft isolation, publish/unpublish, contact updates, and conflict handling.
- Create `backend/tests/RentACar.Tests/Unit/Controllers/AdminPublicContentControllerTests.cs`: cover route-level success and bad-request/conflict responses.
- Modify `frontend/lib/api/admin/types.ts`: add admin content types and `bodyFormat`.
- Create `frontend/lib/api/admin/publicContent.ts`: add admin content API client functions.
- Modify `frontend/lib/api/admin/index.ts`: export public content client functions.
- Modify `frontend/components/layout/sidebar/nav-main.tsx`: add `İçerik Yönetimi` navigation item.
- Modify `frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx`: add settings tab for public content and rename system tab to general public settings if needed.
- Create `frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx`: route entry for the new content-management workspace.
- Create `frontend/components/admin/public-content/PublicContentManager.tsx`: orchestrate page/contact tabs, data loading, dirty state, and save actions.
- Create `frontend/components/admin/public-content/PageContentEditor.tsx`: page list, locale switching, metadata form, blocks, draft/publish actions.
- Create `frontend/components/admin/public-content/ContactContentEditor.tsx`: channels, offices, working hours, and map form.
- Create `frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx`: constrained Tiptap editor for page block bodies.
- Create `frontend/lib/public-content/sanitize-managed-html.ts`: central DOMPurify allowlist helper.
- Modify `frontend/components/public/ManagedPageContent.tsx`: render `plain` and sanitized `html` block bodies safely.
- Modify `frontend/app/(admin)/dashboard/(auth)/settings/system/page.tsx`: remove managed pages and contact editing from the broad system settings screen after the new route is in place.
- Modify `frontend/app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx`: update system-settings tests to match the reduced scope.
- Create `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`: unit/component tests for the new workspace.
- Create `frontend/lib/public-content/sanitize-managed-html.test.ts`: sanitizer allowlist tests.
- Modify `frontend/components/public/ManagedPageContent.test.tsx`: draft isolation and rich/plain rendering tests.
- Modify `frontend/e2e/tests/admin-public-settings.spec.ts`: point the content-management smoke to the new route and assert key controls.
- Modify `frontend/package.json` and lockfile if DOMPurify is not already installed.

## Interfaces

### Backend DTOs

Add these records beside the existing public settings DTOs:

```csharp
public sealed record PublicPagePublishedSnapshotDto(
    string Title,
    string Subtitle,
    string SeoTitle,
    string SeoDescription,
    IReadOnlyList<PublicPageBlockDto> Blocks,
    DateTime? PublishedAtUtc);

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
```

Update `PublicPageBlockDto` to include a backward-compatible format field:

```csharp
public sealed record PublicPageBlockDto(
    string Id,
    string Heading,
    string Body,
    bool IsVisible,
    int SortOrder,
    string BodyFormat = "plain");
```

### Backend Service Interface

Add these methods to `IPublicSiteSettingsService`:

```csharp
Task<AdminPublicContentDto> GetAdminContentAsync(CancellationToken cancellationToken = default);

Task<AdminPublicContentDto> UpdatePageDraftAsync(
    string slug,
    string locale,
    UpdateAdminPublicPageDraftRequest request,
    CancellationToken cancellationToken = default);

Task<AdminPublicContentDto> PublishPageAsync(
    string slug,
    string locale,
    PublishAdminPublicPageRequest request,
    CancellationToken cancellationToken = default);

Task<AdminPublicContentDto> UnpublishPageAsync(
    string slug,
    string locale,
    PublishAdminPublicPageRequest request,
    CancellationToken cancellationToken = default);

Task<AdminPublicContentDto> UpdateContactContentAsync(
    UpdateAdminPublicContactRequest request,
    CancellationToken cancellationToken = default);
```

Use `settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture)` as `Version`. If the request version differs from the current settings version, throw `InvalidOperationException("Public content was updated by another session. Reload before saving.")`.

### Frontend Admin API

Create these functions in `frontend/lib/api/admin/publicContent.ts`:

```ts
export async function getAdminPublicContent(): Promise<AdminPublicContent>;

export async function updateAdminPublicPageDraft(
  slug: string,
  locale: PublicSettingsLocale,
  data: UpdateAdminPublicPageDraftData
): Promise<AdminPublicContent>;

export async function publishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
): Promise<AdminPublicContent>;

export async function unpublishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
): Promise<AdminPublicContent>;

export async function updateAdminPublicContact(
  data: UpdateAdminPublicContactData
): Promise<AdminPublicContent>;
```

### Sanitizer Policy

Create `sanitizeManagedHtml(value: string): string` with this policy:

```ts
const ALLOWED_TAGS = [
  "p",
  "br",
  "strong",
  "em",
  "u",
  "s",
  "ul",
  "ol",
  "li",
  "blockquote",
  "h3",
  "h4",
  "a",
];

const ALLOWED_ATTR = ["href", "target", "rel"];
```

After sanitizing, force external links to `rel="noopener noreferrer"` and strip protocol-relative links. Do not allow `style`, `class`, `id`, `iframe`, `img`, `script`, or event attributes.

---

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

### Task 3: Admin Public Content Controller

**Files:**
- Create: `backend/src/RentACar.API/Controllers/AdminPublicContentController.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Controllers/AdminPublicContentControllerTests.cs`

**Interfaces:**
- Consumes: `IPublicSiteSettingsService` methods from Task 1.
- Produces: admin-only `/api/admin/v1/public-content` endpoint surface.

- [ ] **Step 1: Add controller tests**

Create `AdminPublicContentControllerTests.cs` with:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Controllers;
using RentACar.API.Services;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminPublicContentControllerTests
{
    [Fact]
    public async Task Get_returns_admin_content()
    {
        var service = new Mock<IPublicSiteSettingsService>();
        var content = EmptyContent();
        service.Setup(x => x.GetAdminContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var controller = new AdminPublicContentController(service.Object);

        var result = await controller.Get(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePageDraft_returns_conflict_for_stale_version()
    {
        var service = new Mock<IPublicSiteSettingsService>();
        service.Setup(x => x.UpdatePageDraftAsync("privacy", "tr", It.IsAny<UpdateAdminPublicPageDraftRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Public content was updated by another session. Reload before saving."));
        var controller = new AdminPublicContentController(service.Object);

        var result = await controller.UpdatePageDraft(
            "privacy",
            "tr",
            new UpdateAdminPublicPageDraftRequest("1", "Title", "", "", "", true, 0, []),
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    private static AdminPublicContentDto EmptyContent() =>
        new("1", DateTime.UtcNow, [], [], [], [], "", "", true);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter AdminPublicContentControllerTests --no-restore
```

Expected: compile failure because controller does not exist.

- [ ] **Step 3: Create controller**

Create:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentACar.API.Configuration;
using RentACar.API.Contracts.Common;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[ApiController]
[Route("api/admin/v1/public-content")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
public sealed class AdminPublicContentController(IPublicSiteSettingsService settingsService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var content = await settingsService.GetAdminContentAsync(cancellationToken);
        return OkResponse(content);
    }

    [HttpPut("pages/{slug}/{locale}/draft")]
    public async Task<IActionResult> UpdatePageDraft(
        string slug,
        string locale,
        [FromBody] UpdateAdminPublicPageDraftRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteContentMutationAsync(
            () => settingsService.UpdatePageDraftAsync(slug, locale, request, cancellationToken),
            "Sayfa taslağı güncellendi.");
    }

    [HttpPost("pages/{slug}/{locale}/publish")]
    public async Task<IActionResult> PublishPage(
        string slug,
        string locale,
        [FromBody] PublishAdminPublicPageRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteContentMutationAsync(
            () => settingsService.PublishPageAsync(slug, locale, request, cancellationToken),
            "Sayfa yayınlandı.");
    }

    [HttpPost("pages/{slug}/{locale}/unpublish")]
    public async Task<IActionResult> UnpublishPage(
        string slug,
        string locale,
        [FromBody] PublishAdminPublicPageRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteContentMutationAsync(
            () => settingsService.UnpublishPageAsync(slug, locale, request, cancellationToken),
            "Sayfa yayından kaldırıldı.");
    }

    [HttpPut("contact")]
    public async Task<IActionResult> UpdateContact(
        [FromBody] UpdateAdminPublicContactRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteContentMutationAsync(
            () => settingsService.UpdateContactContentAsync(request, cancellationToken),
            "İletişim içeriği güncellendi.");
    }

    private async Task<IActionResult> ExecuteContentMutationAsync(
        Func<Task<AdminPublicContentDto>> action,
        string message)
    {
        try
        {
            var content = await action();
            return OkResponse(content, message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
```

- [ ] **Step 4: Run controller tests**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter AdminPublicContentControllerTests --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/RentACar.API/Controllers/AdminPublicContentController.cs backend/tests/RentACar.Tests/Unit/Controllers/AdminPublicContentControllerTests.cs
git commit -m "feat(admin): expose public content endpoints"
```

---

### Task 4: Frontend API Types and Client

**Files:**
- Modify: `frontend/lib/api/admin/types.ts`
- Create: `frontend/lib/api/admin/publicContent.ts`
- Modify: `frontend/lib/api/admin/index.ts`
- Test: `frontend/lib/api/admin/admin-api.test.ts`

**Interfaces:**
- Consumes: backend endpoint paths from Task 3.
- Produces: typed frontend admin content client functions.

- [ ] **Step 1: Add API client test**

Add to `admin-api.test.ts`:

```ts
it("updates an admin public page draft through the public-content endpoint", async () => {
  adminPutMock.mockResolvedValueOnce({
    data: { version: "2", updatedAt: "2026-06-27T00:00:00Z", pages: [] },
  });

  const { updateAdminPublicPageDraft } = await import("./publicContent");

  await updateAdminPublicPageDraft("privacy", "tr", {
    version: "1",
    title: "Title",
    subtitle: "",
    seoTitle: "",
    seoDescription: "",
    isPublished: true,
    sortOrder: 0,
    blocks: [],
  });

  expect(adminPutMock).toHaveBeenCalledWith("/v1/public-content/pages/privacy/tr/draft", {
    version: "1",
    title: "Title",
    subtitle: "",
    seoTitle: "",
    seoDescription: "",
    isPublished: true,
    sortOrder: 0,
    blocks: [],
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test lib/api/admin/admin-api.test.ts
```

Expected: FAIL because `publicContent` client does not exist.

- [ ] **Step 3: Add types**

Add these interfaces to `types.ts`:

```ts
export type PublicPageBlockBodyFormat = "plain" | "html";

export interface PublicPagePublishedSnapshot {
  title: string;
  subtitle: string;
  seoTitle: string;
  seoDescription: string;
  blocks: PublicPageBlock[];
  publishedAtUtc: string | null;
}

export interface AdminPublicManagedPage extends PublicManagedPage {
  published: PublicPagePublishedSnapshot | null;
  draftUpdatedAtUtc: string | null;
  publishedAtUtc: string | null;
}

export interface AdminPublicContent {
  version: string;
  updatedAt: string;
  pages: AdminPublicManagedPage[];
  contactPageChannels: PublicContactChannel[];
  contactPageOffices: PublicContactOffice[];
  contactPageWorkingHours: PublicContactWorkingHour[];
  contactPageMapTitle: string;
  contactPageMapEmbedUrl: string;
  contactPageMapIsVisible: boolean;
}

export interface UpdateAdminPublicPageDraftData {
  version: string;
  title: string;
  subtitle: string;
  seoTitle: string;
  seoDescription: string;
  isPublished: boolean;
  sortOrder: number;
  blocks: PublicPageBlock[];
}

export interface UpdateAdminPublicContactData {
  version: string;
  contactPageChannels: PublicContactChannel[];
  contactPageOffices: PublicContactOffice[];
  contactPageWorkingHours: PublicContactWorkingHour[];
  contactPageMapTitle: string;
  contactPageMapEmbedUrl: string;
  contactPageMapIsVisible: boolean;
}
```

Update `PublicPageBlock`:

```ts
export interface PublicPageBlock {
  id: string;
  heading: string;
  body: string;
  isVisible: boolean;
  sortOrder: number;
  bodyFormat?: PublicPageBlockBodyFormat;
}
```

- [ ] **Step 4: Add API client**

Create `publicContent.ts`:

```ts
import { adminGet, adminPost, adminPut } from "../client";
import type {
  AdminResponse,
  AdminPublicContent,
  PublicSettingsLocale,
  UpdateAdminPublicContactData,
  UpdateAdminPublicPageDraftData,
} from "./types";

const PUBLIC_CONTENT_ENDPOINT = "/v1/public-content";

export async function getAdminPublicContent() {
  const response = await adminGet<AdminResponse<AdminPublicContent>>(PUBLIC_CONTENT_ENDPOINT);
  return response.data;
}

export async function updateAdminPublicPageDraft(
  slug: string,
  locale: PublicSettingsLocale,
  data: UpdateAdminPublicPageDraftData
) {
  const response = await adminPut<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/draft`,
    data
  );
  return response.data;
}

export async function publishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
) {
  const response = await adminPost<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/publish`,
    { version }
  );
  return response.data;
}

export async function unpublishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
) {
  const response = await adminPost<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/unpublish`,
    { version }
  );
  return response.data;
}

export async function updateAdminPublicContact(data: UpdateAdminPublicContactData) {
  const response = await adminPut<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/contact`,
    data
  );
  return response.data;
}
```

Export it from `frontend/lib/api/admin/index.ts`:

```ts
export * from "./publicContent";
```

- [ ] **Step 5: Run admin API test**

Run:

```powershell
corepack pnpm -C frontend test lib/api/admin/admin-api.test.ts
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/lib/api/admin/types.ts frontend/lib/api/admin/publicContent.ts frontend/lib/api/admin/index.ts frontend/lib/api/admin/admin-api.test.ts
git commit -m "feat(admin): add public content api client"
```

---

### Task 5: Sanitized Public Rich Text Rendering

**Files:**
- Create: `frontend/lib/public-content/sanitize-managed-html.ts`
- Create: `frontend/lib/public-content/sanitize-managed-html.test.ts`
- Modify: `frontend/components/public/ManagedPageContent.tsx`
- Modify: `frontend/components/public/ManagedPageContent.test.tsx`
- Modify: `frontend/package.json`

**Interfaces:**
- Consumes: `PublicPageBlock.bodyFormat?: "plain" | "html"`.
- Produces: `sanitizeManagedHtml(value: string): string` and safe public rendering for rich text.

- [ ] **Step 1: Install DOMPurify if absent**

Run:

```powershell
corepack pnpm -C frontend add dompurify
corepack pnpm -C frontend add -D @types/dompurify
```

Expected: package and lockfile update. If `@types/dompurify` is unnecessary for the installed version, remove it in the same task.

- [ ] **Step 2: Add sanitizer tests**

Create `sanitize-managed-html.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { sanitizeManagedHtml } from "./sanitize-managed-html";

describe("sanitizeManagedHtml", () => {
  it("keeps approved rich text tags", () => {
    expect(sanitizeManagedHtml("<p>Hello <strong>world</strong></p>")).toBe(
      "<p>Hello <strong>world</strong></p>"
    );
  });

  it("removes script iframe style and event attributes", () => {
    const result = sanitizeManagedHtml(
      '<p style="color:red" onclick="alert(1)">Hello</p><script>alert(1)</script><iframe src="https://example.com"></iframe>'
    );

    expect(result).toBe("<p>Hello</p>");
  });

  it("removes unsafe and protocol-relative links", () => {
    expect(sanitizeManagedHtml('<a href="javascript:alert(1)">bad</a>')).toBe("<a>bad</a>");
    expect(sanitizeManagedHtml('<a href="//example.com">bad</a>')).toBe("<a>bad</a>");
  });

  it("keeps safe links with noopener noreferrer", () => {
    expect(sanitizeManagedHtml('<a href="https://example.com">safe</a>')).toContain(
      'rel="noopener noreferrer"'
    );
  });
});
```

- [ ] **Step 3: Run sanitizer test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test lib/public-content/sanitize-managed-html.test.ts
```

Expected: FAIL because sanitizer helper does not exist.

- [ ] **Step 4: Implement sanitizer helper**

Create `sanitize-managed-html.ts`:

```ts
import DOMPurify from "dompurify";

const SAFE_SCHEMES = /^(https?:|mailto:|tel:)/i;

export function sanitizeManagedHtml(value: string) {
  const clean = DOMPurify.sanitize(value, {
    ALLOWED_TAGS: [
      "p",
      "br",
      "strong",
      "em",
      "u",
      "s",
      "ul",
      "ol",
      "li",
      "blockquote",
      "h3",
      "h4",
      "a",
    ],
    ALLOWED_ATTR: ["href", "target", "rel"],
    FORBID_TAGS: ["script", "style", "iframe", "img", "object", "embed"],
    FORBID_ATTR: ["style", "class", "id"],
  });

  if (typeof window === "undefined") {
    return clean;
  }

  const template = document.createElement("template");
  template.innerHTML = clean;

  template.content.querySelectorAll("a").forEach((link) => {
    const href = link.getAttribute("href") ?? "";
    if (!SAFE_SCHEMES.test(href) || href.startsWith("//")) {
      link.removeAttribute("href");
      link.removeAttribute("target");
      link.removeAttribute("rel");
      return;
    }

    link.setAttribute("rel", "noopener noreferrer");
    if (/^https?:/i.test(href)) {
      link.setAttribute("target", "_blank");
    }
  });

  return template.innerHTML;
}
```

- [ ] **Step 5: Update public renderer**

In `ManagedPageContent.tsx`, keep existing plain text paragraph rendering and add:

```tsx
function ManagedBlockBody({ block }: { block: PublicManagedPage["blocks"][number] }) {
  if (block.bodyFormat === "html") {
    return (
      <div
        className="space-y-4 text-[#475569] [&_a]:font-semibold [&_a]:text-[#0369A1] [&_blockquote]:border-l-4 [&_blockquote]:border-[#CBD5E1] [&_blockquote]:pl-4"
        dangerouslySetInnerHTML={{ __html: sanitizeManagedHtml(block.body) }}
      />
    );
  }

  return (
    <div className="space-y-4 text-[#475569]">
      {splitParagraphs(block.body).map((paragraph) => (
        <p key={paragraph.slice(0, 48)} className="leading-relaxed">
          {paragraph}
        </p>
      ))}
    </div>
  );
}
```

Replace the inline paragraph map with `<ManagedBlockBody block={block} />`.

- [ ] **Step 6: Add renderer tests**

Add assertions to `ManagedPageContent.test.tsx`:

```ts
it("renders html page blocks after sanitizing unsafe content", async () => {
  mockGetPublicSiteSettings.mockResolvedValue({
    pages: [
      {
        id: "tr-privacy",
        slug: "privacy",
        locale: "tr",
        title: "Privacy",
        subtitle: "",
        seoTitle: "",
        seoDescription: "",
        isPublished: true,
        sortOrder: 0,
        blocks: [
          {
            id: "block-1",
            heading: "Body",
            body: '<p>Hello <strong>safe</strong></p><script>alert(1)</script>',
            bodyFormat: "html",
            isVisible: true,
            sortOrder: 0,
          },
        ],
      },
    ],
  });

  render(<ManagedPageContent slug="privacy" />);

  expect(await screen.findByText("safe")).toBeInTheDocument();
  expect(screen.queryByText("alert(1)")).not.toBeInTheDocument();
});
```

- [ ] **Step 7: Run frontend render tests**

Run:

```powershell
corepack pnpm -C frontend test lib/public-content/sanitize-managed-html.test.ts components/public/ManagedPageContent.test.tsx
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add frontend/package.json frontend/pnpm-lock.yaml frontend/lib/public-content/sanitize-managed-html.ts frontend/lib/public-content/sanitize-managed-html.test.ts frontend/components/public/ManagedPageContent.tsx frontend/components/public/ManagedPageContent.test.tsx
git commit -m "fix(public): sanitize managed rich text content"
```

---

### Task 6: Admin Content Route, Navigation, and Loader

**Files:**
- Modify: `frontend/components/layout/sidebar/nav-main.tsx`
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx`
- Create: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx`
- Create: `frontend/components/admin/public-content/PublicContentManager.tsx`
- Test: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`

**Interfaces:**
- Consumes: `getAdminPublicContent()`.
- Produces: visible `/dashboard/settings/public-content` route with page/contact tabs.

- [ ] **Step 1: Add route smoke test**

Create `PublicContentManager.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import PublicContentPage from "./page";

vi.mock("@/lib/api/admin/publicContent", () => ({
  getAdminPublicContent: vi.fn().mockResolvedValue({
    version: "1",
    updatedAt: "2026-06-27T00:00:00Z",
    pages: [],
    contactPageChannels: [],
    contactPageOffices: [],
    contactPageWorkingHours: [],
    contactPageMapTitle: "",
    contactPageMapEmbedUrl: "",
    contactPageMapIsVisible: true,
  }),
}));

describe("PublicContentPage", () => {
  it("renders the public content workspace", async () => {
    render(<PublicContentPage />);

    expect(await screen.findByRole("heading", { name: "İçerik Yönetimi" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Sayfalar" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "İletişim" })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: FAIL because route does not exist.

- [ ] **Step 3: Create route and manager shell**

Create `page.tsx`:

```tsx
"use client";

import PublicContentManager from "@/components/admin/public-content/PublicContentManager";

export default function PublicContentPage() {
  return <PublicContentManager />;
}
```

Create `PublicContentManager.tsx`:

```tsx
"use client";

import useSWR from "swr";
import { FileText, Phone } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { getAdminPublicContent } from "@/lib/api/admin/publicContent";

export default function PublicContentManager() {
  const { data, isLoading, error, mutate } = useSWR(["admin", "public-content"], getAdminPublicContent);

  if (isLoading) {
    return <Skeleton className="h-64 w-full" />;
  }

  if (error || !data) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>İçerik Yönetimi</CardTitle>
        </CardHeader>
        <CardContent>İçerik verisi yüklenemedi.</CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">İçerik Yönetimi</h1>
        <p className="text-sm text-muted-foreground">Public sayfa ve iletişim içeriklerini yönetin.</p>
      </div>
      <Tabs defaultValue="pages" className="space-y-4">
        <TabsList>
          <TabsTrigger value="pages">
            <FileText className="mr-2 h-4 w-4" />
            Sayfalar
          </TabsTrigger>
          <TabsTrigger value="contact">
            <Phone className="mr-2 h-4 w-4" />
            İletişim
          </TabsTrigger>
        </TabsList>
        <TabsContent value="pages">Sayfa editörü yükleniyor.</TabsContent>
        <TabsContent value="contact">İletişim editörü yükleniyor.</TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 4: Add navigation**

Add a settings child item in `nav-main.tsx`:

```ts
{ title: "İçerik Yönetimi", href: "/dashboard/settings/public-content" },
```

Add a settings tab in `settings/layout.tsx`:

```tsx
<TabsTrigger value="public-content" asChild>
  <Link href="/dashboard/settings/public-content">İçerik Yönetimi</Link>
</TabsTrigger>
```

- [ ] **Step 5: Run route test**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/components/layout/sidebar/nav-main.tsx frontend/app/(admin)/dashboard/(auth)/settings/layout.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/page.tsx frontend/components/admin/public-content/PublicContentManager.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
git commit -m "feat(admin): add public content workspace"
```

---

### Task 7: Page Content Editor

**Files:**
- Create: `frontend/components/admin/public-content/PageContentEditor.tsx`
- Create: `frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx`
- Modify: `frontend/components/admin/public-content/PublicContentManager.tsx`
- Test: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`

**Interfaces:**
- Consumes: `AdminPublicContent.pages`, `updateAdminPublicPageDraft`, `publishAdminPublicPage`, `unpublishAdminPublicPage`.
- Produces: page list, locale editor, block editor, draft save, publish/unpublish actions.

- [ ] **Step 1: Add page editor behavior test**

Add:

```tsx
it("saves a selected page draft", async () => {
  const user = userEvent.setup();
  updateAdminPublicPageDraftMock.mockResolvedValue(adminContentFixture);
  getAdminPublicContentMock.mockResolvedValue(adminContentFixture);

  render(<PublicContentPage />);

  await user.click(await screen.findByRole("button", { name: /privacy/i }));
  await user.clear(screen.getByLabelText("Sayfa Başlığı"));
  await user.type(screen.getByLabelText("Sayfa Başlığı"), "Yeni Gizlilik");
  await user.click(screen.getByRole("button", { name: "Taslağı Kaydet" }));

  expect(updateAdminPublicPageDraftMock).toHaveBeenCalledWith(
    "privacy",
    "tr",
    expect.objectContaining({ title: "Yeni Gizlilik", version: "1" })
  );
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: FAIL because the page editor is not implemented.

- [ ] **Step 3: Create constrained rich text editor**

Create `ManagedContentRichTextEditor.tsx`:

```tsx
"use client";

import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import Link from "@tiptap/extension-link";
import Placeholder from "@tiptap/extension-placeholder";
import { Bold, Italic, LinkIcon, List, ListOrdered, Redo, UnderlineIcon, Undo } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type ManagedContentRichTextEditorProps = {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
};

export default function ManagedContentRichTextEditor({
  value,
  onChange,
  placeholder = "İçeriği yazın",
}: ManagedContentRichTextEditorProps) {
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({ codeBlock: false, code: false, horizontalRule: false }),
      Underline,
      Link.configure({
        openOnClick: false,
        autolink: false,
        defaultProtocol: "https",
        isAllowedUri: (url, ctx) =>
          ctx.defaultValidate(url) &&
          /^(https?:|mailto:|tel:)/i.test(url) &&
          !url.startsWith("//"),
      }),
      Placeholder.configure({ placeholder }),
    ],
    content: value || "<p></p>",
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
  });

  if (!editor) {
    return null;
  }

  return (
    <div className="rounded-md border bg-background">
      <div className="flex flex-wrap gap-1 border-b p-2">
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleBold().run()} aria-label="Kalın">
          <Bold className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleItalic().run()} aria-label="İtalik">
          <Italic className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleUnderline().run()} aria-label="Altı çizili">
          <UnderlineIcon className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleBulletList().run()} aria-label="Madde listesi">
          <List className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().toggleOrderedList().run()} aria-label="Numaralı liste">
          <ListOrdered className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          onClick={() => {
            const href = window.prompt("Link URL");
            if (href && /^(https?:|mailto:|tel:)/i.test(href) && !href.startsWith("//")) {
              editor.chain().focus().setLink({ href }).run();
            }
          }}
          aria-label="Link ekle"
        >
          <LinkIcon className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().undo().run()} aria-label="Geri al">
          <Undo className="h-4 w-4" />
        </Button>
        <Button type="button" variant="ghost" size="icon" onClick={() => editor.chain().focus().redo().run()} aria-label="Yinele">
          <Redo className="h-4 w-4" />
        </Button>
      </div>
      <EditorContent editor={editor} className={cn("min-h-48 cursor-text px-3 py-2")} />
    </div>
  );
}
```

- [ ] **Step 4: Create page editor**

Create `PageContentEditor.tsx` with these core behaviors:

```tsx
"use client";

import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import type { AdminPublicContent, AdminPublicManagedPage, PublicSettingsLocale } from "@/lib/api/admin/types";
import { publishAdminPublicPage, unpublishAdminPublicPage, updateAdminPublicPageDraft } from "@/lib/api/admin/publicContent";
import ManagedContentRichTextEditor from "./ManagedContentRichTextEditor";

const locales: PublicSettingsLocale[] = ["tr", "en", "ru", "ar", "de"];

type PageContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

export default function PageContentEditor({ content, onContentChange }: PageContentEditorProps) {
  const [selectedSlug, setSelectedSlug] = useState(content.pages[0]?.slug ?? "privacy");
  const [selectedLocale, setSelectedLocale] = useState<PublicSettingsLocale>("tr");
  const page = content.pages.find((item) => item.slug === selectedSlug && item.locale === selectedLocale);
  const [draft, setDraft] = useState<AdminPublicManagedPage | null>(page ?? null);

  const slugs = useMemo(() => Array.from(new Set(content.pages.map((item) => item.slug))).sort(), [content.pages]);

  if (!draft) {
    return <div className="rounded-md border p-4 text-sm text-muted-foreground">Düzenlenecek sayfa seçin.</div>;
  }

  async function saveDraft() {
    const next = await updateAdminPublicPageDraft(draft.slug, draft.locale as PublicSettingsLocale, {
      version: content.version,
      title: draft.title,
      subtitle: draft.subtitle,
      seoTitle: draft.seoTitle,
      seoDescription: draft.seoDescription,
      isPublished: draft.isPublished,
      sortOrder: draft.sortOrder,
      blocks: draft.blocks.map((block, index) => ({ ...block, sortOrder: index, bodyFormat: block.bodyFormat ?? "html" })),
    });
    onContentChange(next);
    toast.success("Sayfa taslağı kaydedildi.");
  }

  async function publishDraft() {
    const next = await publishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, content.version);
    onContentChange(next);
    toast.success("Sayfa yayınlandı.");
  }

  async function unpublishDraft() {
    const next = await unpublishAdminPublicPage(draft.slug, draft.locale as PublicSettingsLocale, content.version);
    onContentChange(next);
    toast.success("Sayfa yayından kaldırıldı.");
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
      <div className="space-y-2 rounded-md border p-3">
        {slugs.map((slug) => (
          <Button key={slug} type="button" variant={slug === selectedSlug ? "default" : "ghost"} className="w-full justify-start" onClick={() => setSelectedSlug(slug)}>
            {slug}
          </Button>
        ))}
      </div>
      <div className="space-y-4 rounded-md border p-4">
        <div className="flex flex-wrap gap-2">
          {locales.map((locale) => (
            <Button key={locale} type="button" variant={locale === selectedLocale ? "default" : "outline"} onClick={() => setSelectedLocale(locale)}>
              {locale.toUpperCase()}
            </Button>
          ))}
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="page-title">Sayfa Başlığı</Label>
            <Input id="page-title" value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} />
          </div>
          <div className="flex items-center gap-2 pt-7">
            <Switch checked={draft.isPublished} onCheckedChange={(checked) => setDraft({ ...draft, isPublished: checked })} />
            <span className="text-sm">Yayında</span>
          </div>
        </div>
        {draft.blocks.map((block, index) => (
          <div key={block.id} className="space-y-2 rounded-md border p-3">
            <Label htmlFor={`block-heading-${block.id}`}>Bölüm Başlığı</Label>
            <Input
              id={`block-heading-${block.id}`}
              value={block.heading}
              onChange={(event) =>
                setDraft({
                  ...draft,
                  blocks: draft.blocks.map((item, itemIndex) => (itemIndex === index ? { ...item, heading: event.target.value } : item)),
                })
              }
            />
            <ManagedContentRichTextEditor
              value={block.body}
              onChange={(body) =>
                setDraft({
                  ...draft,
                  blocks: draft.blocks.map((item, itemIndex) => (itemIndex === index ? { ...item, body, bodyFormat: "html" } : item)),
                })
              }
            />
          </div>
        ))}
        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={saveDraft}>Taslağı Kaydet</Button>
          <Button type="button" variant="outline" onClick={publishDraft}>Yayınla</Button>
          <Button type="button" variant="outline" onClick={unpublishDraft}>Yayından Kaldır</Button>
        </div>
      </div>
    </div>
  );
}
```

Wire it into `PublicContentManager` pages tab:

```tsx
<TabsContent value="pages">
  <PageContentEditor content={data} onContentChange={(next) => mutate(next, false)} />
</TabsContent>
```

- [ ] **Step 5: Run page editor tests**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/components/admin/public-content/PageContentEditor.tsx frontend/components/admin/public-content/ManagedContentRichTextEditor.tsx frontend/components/admin/public-content/PublicContentManager.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
git commit -m "feat(admin): add page content editor"
```

---

### Task 8: Contact Content Editor

**Files:**
- Create: `frontend/components/admin/public-content/ContactContentEditor.tsx`
- Modify: `frontend/components/admin/public-content/PublicContentManager.tsx`
- Test: `frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx`

**Interfaces:**
- Consumes: `AdminPublicContent.contactPageChannels`, offices, working hours, map fields.
- Produces: editable contact content form using `updateAdminPublicContact`.

- [ ] **Step 1: Add contact save test**

Add:

```tsx
it("saves contact map content", async () => {
  const user = userEvent.setup();
  updateAdminPublicContactMock.mockResolvedValue(adminContentFixture);
  getAdminPublicContentMock.mockResolvedValue(adminContentFixture);

  render(<PublicContentPage />);

  await user.click(await screen.findByRole("tab", { name: "İletişim" }));
  await user.clear(screen.getByLabelText("Harita Başlığı"));
  await user.type(screen.getByLabelText("Harita Başlığı"), "Alanya Ofisleri");
  await user.click(screen.getByRole("button", { name: "İletişimi Kaydet" }));

  expect(updateAdminPublicContactMock).toHaveBeenCalledWith(
    expect.objectContaining({ version: "1", contactPageMapTitle: "Alanya Ofisleri" })
  );
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: FAIL because contact editor is not implemented.

- [ ] **Step 3: Implement contact editor**

Create `ContactContentEditor.tsx`:

```tsx
"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import type { AdminPublicContent } from "@/lib/api/admin/types";
import { updateAdminPublicContact } from "@/lib/api/admin/publicContent";

type ContactContentEditorProps = {
  content: AdminPublicContent;
  onContentChange: (content: AdminPublicContent) => void;
};

export default function ContactContentEditor({ content, onContentChange }: ContactContentEditorProps) {
  const [mapTitle, setMapTitle] = useState(content.contactPageMapTitle);
  const [mapEmbedUrl, setMapEmbedUrl] = useState(content.contactPageMapEmbedUrl);
  const [mapVisible, setMapVisible] = useState(content.contactPageMapIsVisible);

  async function saveContact() {
    const next = await updateAdminPublicContact({
      version: content.version,
      contactPageChannels: content.contactPageChannels,
      contactPageOffices: content.contactPageOffices,
      contactPageWorkingHours: content.contactPageWorkingHours,
      contactPageMapTitle: mapTitle,
      contactPageMapEmbedUrl: mapEmbedUrl,
      contactPageMapIsVisible: mapVisible,
    });
    onContentChange(next);
    toast.success("İletişim içeriği kaydedildi.");
  }

  return (
    <div className="space-y-4 rounded-md border p-4">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="contact-map-title">Harita Başlığı</Label>
          <Input id="contact-map-title" value={mapTitle} onChange={(event) => setMapTitle(event.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="contact-map-url">Google Maps Embed URL</Label>
          <Input id="contact-map-url" value={mapEmbedUrl} onChange={(event) => setMapEmbedUrl(event.target.value)} />
        </div>
      </div>
      <div className="flex items-center gap-2">
        <Switch checked={mapVisible} onCheckedChange={setMapVisible} />
        <span className="text-sm">Harita görünsün</span>
      </div>
      <div className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
        Kanal, ofis ve çalışma saati listeleri mevcut veri modelinden yüklendi. Bu task harita kaydını bağlar; liste satırı düzenleme aynı component içinde sonraki küçük adımda genişletilir.
      </div>
      <Button type="button" onClick={saveContact}>İletişimi Kaydet</Button>
    </div>
  );
}
```

Wire it into `PublicContentManager`:

```tsx
<TabsContent value="contact">
  <ContactContentEditor content={data} onContentChange={(next) => mutate(next, false)} />
</TabsContent>
```

- [ ] **Step 4: Extend contact editor list rows**

Add editable row components inside `ContactContentEditor` for:

```ts
contactPageChannels: { label, value, href, description, type, isVisible }
contactPageOffices: { name, address, phone, hours, type, isVisible }
contactPageWorkingHours: { day, hours, isVisible }
```

Each row must use `Input` and `Switch`, and update local state arrays before `saveContact()`. The saved payload must include the edited arrays.

- [ ] **Step 5: Run contact tests**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/components/admin/public-content/ContactContentEditor.tsx frontend/components/admin/public-content/PublicContentManager.tsx frontend/app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx
git commit -m "feat(admin): add contact content editor"
```

---

### Task 9: Remove Duplicate Content Editing from System Settings

**Files:**
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/system/page.tsx`
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx`
- Modify: `frontend/e2e/tests/admin-public-settings.spec.ts`

**Interfaces:**
- Consumes: public content workspace from Tasks 6-8.
- Produces: system settings screen that no longer embeds managed pages/contact editing.

- [ ] **Step 1: Update system settings test expectations**

Change tests so `SystemSettingsPage` still saves company/link/social fields but no longer expects:

```ts
screen.getByText("Sayfalar")
screen.getByText("İletişim Sayfası Kanalları")
screen.getByText("İletişim Sayfası Ofisleri")
screen.getByText("İletişim Sayfası Çalışma Saatleri")
```

Add:

```ts
expect(screen.queryByText("Sayfalar")).not.toBeInTheDocument();
expect(screen.queryByText("İletişim Sayfası Kanalları")).not.toBeInTheDocument();
```

- [ ] **Step 2: Run system test to verify it fails**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx"
```

Expected: FAIL because old sections still render.

- [ ] **Step 3: Remove managed page/contact sections**

In `system/page.tsx`, remove:

- `managedPageSchema`, `pageBlockSchema`, page helper functions, and page field-array rendering.
- Contact page channels/offices/working-hours/map rendering and field arrays.
- UI markers `Sayfalar`, `İletişim Sayfası Kanalları`, `İletişim Sayfası Ofisleri`, `İletişim Sayfası Çalışma Saatleri`, `İletişim Sayfası Haritası`.

Keep company identity and global public navigation/link/social settings.

- [ ] **Step 4: Update e2e route**

In `admin-public-settings.spec.ts`, replace:

```ts
await page.goto("/dashboard/settings/system");
```

with:

```ts
await page.goto("/dashboard/settings/public-content");
```

Update expected visible texts:

```ts
await expect(page.getByRole("heading", { name: "İçerik Yönetimi" })).toBeVisible();
await expect(page.getByRole("tab", { name: "Sayfalar" })).toBeVisible();
await expect(page.getByRole("tab", { name: "İletişim" })).toBeVisible();
```

- [ ] **Step 5: Run affected frontend tests**

Run:

```powershell
corepack pnpm -C frontend test "app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx" "app/(admin)/dashboard/(auth)/settings/public-content/PublicContentManager.test.tsx"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add frontend/app/(admin)/dashboard/(auth)/settings/system/page.tsx frontend/app/(admin)/dashboard/(auth)/settings/system/SystemSettingsPage.test.tsx frontend/e2e/tests/admin-public-settings.spec.ts
git commit -m "refactor(admin): separate public content from system settings"
```

---

### Task 10: Verification, Security Scan, and Handoff

**Files:**
- Modify only if verification finds concrete failures in files touched by prior tasks.

**Interfaces:**
- Consumes: all tasks above.
- Produces: verified implementation ready for review.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter "PublicSiteSettings|AdminPublicContent" --no-restore
```

Expected: PASS.

- [ ] **Step 2: Run frontend tests**

Run:

```powershell
corepack pnpm -C frontend test
```

Expected: PASS.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
corepack pnpm -C frontend build
```

Expected: PASS.

- [ ] **Step 4: Run TypeScript check if build does not cover it**

Run:

```powershell
corepack pnpm -C frontend exec tsc --noEmit
```

Expected: PASS.

- [ ] **Step 5: Run Aikido scan**

Run `aikido_full_scan` on each generated, added, and modified first-party code file. Include full file content. Fix any reported issues and rerun until the scan reports zero issues for the modified scope.

- [ ] **Step 6: Update execution docs**

Append a short dated entry to `docs/10_Execution_Tracking.md`:

```markdown
**27 Jun 2026 Admin Public Content Management:** Admin content management moved to `/dashboard/settings/public-content` with draft/publish page workflow, sanitized rich text rendering, and separated contact editing. Verification: `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter "PublicSiteSettings|AdminPublicContent" --no-restore` PASS, `corepack pnpm -C frontend test` PASS, `corepack pnpm -C frontend build` PASS, `corepack pnpm -C frontend exec tsc --noEmit` PASS, Aikido modified-file scan PASS with 0 issues.
```

Only append this entry after every listed command and scan has the matching PASS result. If any verification fails, fix the failure first and record the final passing command outcomes instead of this sentence.

- [ ] **Step 7: Final commit**

```powershell
git add docs/10_Execution_Tracking.md
git commit -m "docs(admin): record public content management verification"
```

## Self-Review

- Spec coverage: The plan covers separate admin route, page/contact scope, real draft/publish behavior, public draft isolation, rich text editor constraints, sanitizer policy, tests, build, and Aikido scan.
- Placeholder scan: The plan defines concrete interfaces, commands, and expected results without deferred fill-in markers.
- Type consistency: Backend DTO names match service signatures; frontend API types match client functions; `bodyFormat` is consistently `"plain" | "html"`.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-27-admin-public-content-management.md`. Two execution options:

1. **Subagent-Driven (recommended)** - Dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.
