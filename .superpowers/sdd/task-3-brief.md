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

