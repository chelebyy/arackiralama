# Task 3 Review Package

Base: 160e00893d54947c9c95ad27950e183df6fd68ed
Head: ddbb8266e55a3ea2458f97e37a663ba58f9b10c8

## Commit Log
```
ddbb826 feat(admin): expose public content endpoints
```

## Diff Stat
```
 .../Controllers/AdminPublicContentController.cs    |  87 ++++++++++
 .../AdminPublicContentControllerTests.cs           | 182 +++++++++++++++++++++
 2 files changed, 269 insertions(+)
```

## Diff
```diff
diff --git a/backend/src/RentACar.API/Controllers/AdminPublicContentController.cs b/backend/src/RentACar.API/Controllers/AdminPublicContentController.cs
new file mode 100644
index 0000000..2654e10
--- /dev/null
+++ b/backend/src/RentACar.API/Controllers/AdminPublicContentController.cs
@@ -0,0 +1,87 @@
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Mvc;
+using Microsoft.AspNetCore.RateLimiting;
+using RentACar.API.Configuration;
+using RentACar.API.Contracts;
+using RentACar.API.Contracts.PublicSiteSettings;
+using RentACar.API.Services;
+
+namespace RentACar.API.Controllers;
+
+[Route("api/admin/v1/public-content")]
+[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
+[EnableRateLimiting(RateLimitPolicyNames.Standard)]
+public sealed class AdminPublicContentController(IPublicSiteSettingsService settingsService) : BaseApiController
+{
+    [HttpGet]
+    public async Task<IActionResult> Get(CancellationToken cancellationToken)
+    {
+        var content = await settingsService.GetAdminContentAsync(cancellationToken);
+        return OkResponse(content);
+    }
+
+    [HttpPut("pages/{slug}/{locale}/draft")]
+    public async Task<IActionResult> UpdatePageDraft(
+        string slug,
+        string locale,
+        [FromBody] UpdateAdminPublicPageDraftRequest request,
+        CancellationToken cancellationToken)
+    {
+        return await ExecuteContentMutationAsync(
+            () => settingsService.UpdatePageDraftAsync(slug, locale, request, cancellationToken),
+            "Sayfa taslağı güncellendi.");
+    }
+
+    [HttpPost("pages/{slug}/{locale}/publish")]
+    public async Task<IActionResult> PublishPage(
+        string slug,
+        string locale,
+        [FromBody] PublishAdminPublicPageRequest request,
+        CancellationToken cancellationToken)
+    {
+        return await ExecuteContentMutationAsync(
+            () => settingsService.PublishPageAsync(slug, locale, request, cancellationToken),
+            "Sayfa yayınlandı.");
+    }
+
+    [HttpPost("pages/{slug}/{locale}/unpublish")]
+    public async Task<IActionResult> UnpublishPage(
+        string slug,
+        string locale,
+        [FromBody] PublishAdminPublicPageRequest request,
+        CancellationToken cancellationToken)
+    {
+        return await ExecuteContentMutationAsync(
+            () => settingsService.UnpublishPageAsync(slug, locale, request, cancellationToken),
+            "Sayfa yayından kaldırıldı.");
+    }
+
+    [HttpPut("contact")]
+    public async Task<IActionResult> UpdateContact(
+        [FromBody] UpdateAdminPublicContactRequest request,
+        CancellationToken cancellationToken)
+    {
+        return await ExecuteContentMutationAsync(
+            () => settingsService.UpdateContactContentAsync(request, cancellationToken),
+            "İletişim içeriği güncellendi.");
+    }
+
+    private async Task<IActionResult> ExecuteContentMutationAsync(
+        Func<Task<AdminPublicContentDto>> action,
+        string message)
+    {
+        try
+        {
+            var content = await action();
+            return OkResponse(content, message);
+        }
+        catch (InvalidOperationException ex)
+        {
+            return Conflict(ApiResponse<object>.Fail(ex.Message));
+        }
+        catch (ArgumentException ex)
+        {
+            return BadRequest(ApiResponse<object>.Fail(ex.Message));
+        }
+    }
+}
diff --git a/backend/tests/RentACar.Tests/Unit/Controllers/AdminPublicContentControllerTests.cs b/backend/tests/RentACar.Tests/Unit/Controllers/AdminPublicContentControllerTests.cs
new file mode 100644
index 0000000..ad47d69
--- /dev/null
+++ b/backend/tests/RentACar.Tests/Unit/Controllers/AdminPublicContentControllerTests.cs
@@ -0,0 +1,182 @@
+using System.Reflection;
+using FluentAssertions;
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Mvc;
+using Moq;
+using RentACar.API.Configuration;
+using RentACar.API.Contracts;
+using RentACar.API.Contracts.PublicSiteSettings;
+using RentACar.API.Controllers;
+using RentACar.API.Services;
+using Xunit;
+
+namespace RentACar.Tests.Unit.Controllers;
+
+public sealed class AdminPublicContentControllerTests
+{
+    [Fact]
+    public async Task Get_ReturnsAdminContent()
+    {
+        var serviceMock = new Mock<IPublicSiteSettingsService>();
+        var content = EmptyContent();
+        serviceMock.Setup(s => s.GetAdminContentAsync(It.IsAny<CancellationToken>()))
+            .ReturnsAsync(content);
+        var controller = CreateController(serviceMock.Object);
+
+        var result = await controller.Get(CancellationToken.None);
+
+        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
+        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
+        response.Success.Should().BeTrue();
+        response.Data.Should().BeSameAs(content);
+    }
+
+    [Fact]
+    public async Task UpdatePageDraft_ReturnsConflictForStaleVersion()
+    {
+        var request = DraftRequest("1");
+        var serviceMock = new Mock<IPublicSiteSettingsService>();
+        serviceMock
+            .Setup(s => s.UpdatePageDraftAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
+            .ThrowsAsync(new InvalidOperationException("Public content was updated by another session. Reload before saving."));
+        var controller = CreateController(serviceMock.Object);
+
+        var result = await controller.UpdatePageDraft("privacy", "tr", request, CancellationToken.None);
+
+        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
+        var response = conflictResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
+        response.Success.Should().BeFalse();
+        response.Message.Should().Contain("updated by another session");
+    }
+
+    [Fact]
+    public async Task UpdatePageDraft_ReturnsBadRequestForInvalidPayload()
+    {
+        var request = DraftRequest("1");
+        var serviceMock = new Mock<IPublicSiteSettingsService>();
+        serviceMock
+            .Setup(s => s.UpdatePageDraftAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
+            .ThrowsAsync(new ArgumentException("Title is required."));
+        var controller = CreateController(serviceMock.Object);
+
+        var result = await controller.UpdatePageDraft("privacy", "tr", request, CancellationToken.None);
+
+        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
+        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
+        response.Success.Should().BeFalse();
+        response.Message.Should().Be("Title is required.");
+    }
+
+    [Fact]
+    public async Task PublishPage_RoutesToService()
+    {
+        var request = new PublishAdminPublicPageRequest("1");
+        var content = EmptyContent();
+        var serviceMock = new Mock<IPublicSiteSettingsService>();
+        serviceMock
+            .Setup(s => s.PublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
+            .ReturnsAsync(content);
+        var controller = CreateController(serviceMock.Object);
+
+        var result = await controller.PublishPage("privacy", "tr", request, CancellationToken.None);
+
+        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
+        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
+        response.Data.Should().BeSameAs(content);
+        response.Message.Should().Contain("yayınlandı");
+        serviceMock.Verify(
+            s => s.PublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()),
+            Times.Once);
+    }
+
+    [Fact]
+    public async Task UnpublishPage_RoutesToService()
+    {
+        var request = new PublishAdminPublicPageRequest("1");
+        var content = EmptyContent();
+        var serviceMock = new Mock<IPublicSiteSettingsService>();
+        serviceMock
+            .Setup(s => s.UnpublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
+            .ReturnsAsync(content);
+        var controller = CreateController(serviceMock.Object);
+
+        var result = await controller.UnpublishPage("privacy", "tr", request, CancellationToken.None);
+
+        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
+        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
+        response.Data.Should().BeSameAs(content);
+        response.Message.Should().Contain("yayından kaldırıldı");
+        serviceMock.Verify(
+            s => s.UnpublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()),
+            Times.Once);
+    }
+
+    [Fact]
+    public async Task UpdateContact_RoutesToService()
+    {
+        var request = ContactRequest("1");
+        var content = EmptyContent();
+        var serviceMock = new Mock<IPublicSiteSettingsService>();
+        serviceMock
+            .Setup(s => s.UpdateContactContentAsync(request, It.IsAny<CancellationToken>()))
+            .ReturnsAsync(content);
+        var controller = CreateController(serviceMock.Object);
+
+        var result = await controller.UpdateContact(request, CancellationToken.None);
+
+        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
+        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
+        response.Data.Should().BeSameAs(content);
+        response.Message.Should().Contain("İletişim");
+        serviceMock.Verify(s => s.UpdateContactContentAsync(request, It.IsAny<CancellationToken>()), Times.Once);
+    }
+
+    [Fact]
+    public void Controller_RequiresSuperAdminPolicy()
+    {
+        var attribute = typeof(AdminPublicContentController)
+            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
+            .Single();
+
+        attribute.Policy.Should().Be(AuthPolicyNames.SuperAdminOnly);
+    }
+
+    private static AdminPublicContentController CreateController(IPublicSiteSettingsService service)
+    {
+        return new AdminPublicContentController(service)
+        {
+            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
+        };
+    }
+
+    private static AdminPublicContentDto EmptyContent() => new(
+        "1",
+        DateTime.UtcNow,
+        [],
+        [],
+        [],
+        [],
+        "",
+        "",
+        true);
+
+    private static UpdateAdminPublicPageDraftRequest DraftRequest(string version) => new(
+        version,
+        "Title",
+        "",
+        "",
+        "",
+        true,
+        0,
+        []);
+
+    private static UpdateAdminPublicContactRequest ContactRequest(string version) => new(
+        version,
+        [],
+        [],
+        [],
+        "Map",
+        "https://www.google.com/maps/embed?pb=managed",
+        true);
+}
```
