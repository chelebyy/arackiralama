using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RentACar.API.Contracts;
using RentACar.API.Contracts.PublicSiteSettings;
using RentACar.API.Controllers;
using RentACar.API.Services;
using Xunit;

namespace RentACar.Tests.Unit.Controllers;

public sealed class AdminPublicSiteSettingsControllerTests
{
    [Fact]
    public async Task Get_ReturnsPublicSiteSettings()
    {
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings());

        var controller = CreateController(serviceMock.Object);

        var result = await controller.Get(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PublicSiteSettingsDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.QuickLinks.Should().ContainSingle(x => x.Id == "vehicles");
    }

    [Fact]
    public async Task Update_WithValidPayload_ReturnsOk()
    {
        var request = new UpdatePublicSiteSettingsRequest(
            "Alanya",
            "Center",
            "+90 555",
            "contact@example.test",
            "09:00 - 18:00",
            [new PublicSiteLinkDto("home", "Ana Sayfa", "/", true, 0)],
            [new PublicSiteLinkDto("ctaPrimary", "Araçları İncele", "/vehicles", true, 0)],
            [new PublicSiteLinkDto("vehicles", "Araçlar", "/vehicles", true, 0)],
            [new PublicSocialLinkDto("instagram", "Instagram", "https://instagram.com/alanya", true, 0)],
            [new PublicSiteLinkDto("contact", "İletişim", "/contact", true, 0)],
            [new PublicContactChannelDto("phone", "phone", "Rezervasyon", "+90 555", "tel:+90555", "Ara", true, 0)],
            [new PublicContactOfficeDto("main", "Ana Ofis", "Center", "+90 555", "09:00 - 18:00", "main", true, 0)],
            [new PublicContactWorkingHourDto("weekday", "Hafta içi", "09:00 - 18:00", true, 0)],
            "Map",
            "https://www.google.com/maps/embed?pb=managed",
            true,
            [CreateManagedPage()]);
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock.Setup(s => s.UpdateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings());

        var controller = CreateController(serviceMock.Object);

        var result = await controller.Update(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PublicSiteSettingsDto>>().Subject;
        response.Message.Should().Contain("güncellendi");
    }

    [Fact]
    public async Task Update_WhenValidationFails_ReturnsBadRequest()
    {
        var request = new UpdatePublicSiteSettingsRequest("", "", "", "", "", [], [], [], [], [], [], [], [], "", "", true, []);
        var serviceMock = new Mock<IPublicSiteSettingsService>();
        serviceMock.Setup(s => s.UpdateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Şirket adı zorunludur."));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.Update(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AdminPublicSiteSettingsController CreateController(IPublicSiteSettingsService service)
    {
        return new AdminPublicSiteSettingsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static PublicSiteSettingsDto CreateSettings() => new(
        "Alanya",
        "Center",
        "+90 555",
        "contact@example.test",
        "09:00 - 18:00",
        [new PublicSiteLinkDto("home", "Ana Sayfa", "/", true, 0)],
        [new PublicSiteLinkDto("ctaPrimary", "Araçları İncele", "/vehicles", true, 0)],
        [new PublicSiteLinkDto("vehicles", "Araçlar", "/vehicles", true, 0)],
        [new PublicSocialLinkDto("instagram", "Instagram", "https://instagram.com/alanya", true, 0)],
        [new PublicSiteLinkDto("contact", "İletişim", "/contact", true, 0)],
        [new PublicContactChannelDto("phone", "phone", "Rezervasyon", "+90 555", "tel:+90555", "Ara", true, 0)],
        [new PublicContactOfficeDto("main", "Ana Ofis", "Center", "+90 555", "09:00 - 18:00", "main", true, 0)],
        [new PublicContactWorkingHourDto("weekday", "Hafta içi", "09:00 - 18:00", true, 0)],
        "Map",
        "https://www.google.com/maps/embed?pb=managed",
        true,
        [CreateManagedPage()],
        new PublicPaymentMethodsDto(true, true, true, false, true),
        false,
        DateTime.UtcNow);

    private static PublicManagedPageDto CreateManagedPage() => new(
        "tr-about",
        "about",
        "tr",
        "Hakkımızda",
        "Kurumsal bilgi",
        "Hakkımızda",
        "Kurumsal bilgi",
        true,
        0,
        [new PublicPageBlockDto("story", "Hikaye", "İçerik", true, 0)]);
}
