using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Configuration;
using RentACar.API.Contracts;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminPublicContentControllerTests
{
    [Fact]
    public async Task Get_ReturnsAdminContent()
    {
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        var content = EmptyContent();
        serviceMock.Setup(s => s.GetAdminContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.Get(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeSameAs(content);
    }

    [Fact]
    public async Task UpdatePageDraft_ReturnsConflictForStaleVersion()
    {
        var request = DraftRequest("1");
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock
            .Setup(s => s.UpdatePageDraftAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Public content was updated by another session. Reload before saving."));
        var controller = CreateController(serviceMock.Object);

        var result = await controller.UpdatePageDraft("privacy", "tr", request, CancellationToken.None);

        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var response = conflictResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("updated by another session");
    }

    [Fact]
    public async Task UpdatePageDraft_ReturnsBadRequestForInvalidPayload()
    {
        var request = DraftRequest("1");
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock
            .Setup(s => s.UpdatePageDraftAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Title is required."));
        var controller = CreateController(serviceMock.Object);

        var result = await controller.UpdatePageDraft("privacy", "tr", request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequest.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Title is required.");
    }

    [Fact]
    public async Task PublishPage_RoutesToService()
    {
        var request = new PublishAdminPublicPageRequest("1");
        var content = EmptyContent();
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock
            .Setup(s => s.PublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.PublishPage("privacy", "tr", request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
        response.Data.Should().BeSameAs(content);
        response.Message.Should().Contain("yayınlandı");
        serviceMock.Verify(
            s => s.PublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnpublishPage_RoutesToService()
    {
        var request = new PublishAdminPublicPageRequest("1");
        var content = EmptyContent();
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock
            .Setup(s => s.UnpublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.UnpublishPage("privacy", "tr", request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
        response.Data.Should().BeSameAs(content);
        response.Message.Should().Contain("yayından kaldırıldı");
        serviceMock.Verify(
            s => s.UnpublishPageAsync("privacy", "tr", request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateContact_RoutesToService()
    {
        var request = ContactRequest("1");
        var content = EmptyContent();
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock
            .Setup(s => s.UpdateContactContentAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.UpdateContact(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<AdminPublicContentDto>>().Subject;
        response.Data.Should().BeSameAs(content);
        response.Message.Should().Contain("İletişim");
        serviceMock.Verify(s => s.UpdateContactContentAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Controller_RequiresSuperAdminPolicy()
    {
        var attribute = typeof(AdminPublicContentController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();

        attribute.Policy.Should().Be(AuthPolicyNames.SuperAdminOnly);
    }

    private static AdminPublicContentController CreateController(IPublicSiteSettingsService service)
    {
        return new AdminPublicContentController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AdminPublicContentDto EmptyContent() => new(
        "1",
        DateTime.UtcNow,
        [],
        [],
        [],
        [],
        "",
        "",
        true);

    private static UpdateAdminPublicPageDraftRequest DraftRequest(string version) => new(
        version,
        "Title",
        "",
        "",
        "",
        true,
        0,
        []);

    private static UpdateAdminPublicContactRequest ContactRequest(string version) => new(
        version,
        [],
        [],
        [],
        "Map",
        "https://www.google.com/maps/embed?pb=managed",
        true);
}
