using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Services;

namespace RentACar.API.Controllers;

[Route("api/admin/v1/public-content")]
[Authorize(Policy = AuthPolicyNames.SuperAdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Standard)]
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
