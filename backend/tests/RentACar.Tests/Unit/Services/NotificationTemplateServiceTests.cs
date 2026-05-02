using FluentAssertions;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class NotificationTemplateServiceTests
{
    private readonly NotificationTemplateService _sut = new();

    [Fact]
    public void RenderEmail_WhenPasswordResetCustomerEnUS_ReturnsCorrectSubjectAndBody()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.PasswordResetCustomer,
            Locale = "en-US",
            Variables = new Dictionary<string, string> { ["Token"] = "abc123", ["ExpiresAtUtc"] = "2030-01-01" }
        };

        var result = _sut.RenderEmail(request);

        result.Subject.Should().Be("Password reset request");
        result.PlainTextBody.Should().Contain("Token: abc123");
        result.HtmlBody.Should().Contain("<strong>Token:</strong> abc123");
    }

    [Fact]
    public void RenderEmail_WhenReservationConfirmedTrTR_ReturnsTurkishText()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.ReservationConfirmed,
            Locale = "tr-TR",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R12345" }
        };

        var result = _sut.RenderEmail(request);

        result.Subject.Should().Be("Rezervasyon onaylandi");
        result.PlainTextBody.Should().Contain("Kod: R12345");
    }

    [Fact]
    public void RenderEmail_WhenUnsupportedLocale_FallsBackToTrTR()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.ReservationCancelled,
            Locale = "fr-FR",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R999" }
        };

        var result = _sut.RenderEmail(request);

        result.Subject.Should().Be("Rezervasyon iptal edildi");
        result.PlainTextBody.Should().Contain("Kod: R999");
    }

    [Fact]
    public void RenderEmail_WhenEmptyLocale_FallsBackToTrTR()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.PaymentReceived,
            Locale = "",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R001" }
        };

        var result = _sut.RenderEmail(request);

        result.Subject.Should().Be("Odeme alindi");
    }

    [Fact]
    public void RenderEmail_WhenNullLocale_FallsBackToTrTR()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.DepositReleased,
            Locale = null!,
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R002" }
        };

        var result = _sut.RenderEmail(request);

        result.Subject.Should().Be("Depozito serbest birakildi");
    }

    [Fact]
    public void RenderEmail_WhenInvalidTemplateKey_ThrowsInvalidOperationException()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = "NonExistentKey",
            Locale = "en-US",
            Variables = new Dictionary<string, string>()
        };

        Action act = () => _sut.RenderEmail(request);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Email template could not be resolved*");
    }

    [Fact]
    public void RenderEmail_WhenVariablesMissing_LeavesPlaceholderUnchanged()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.PickupReminder,
            Locale = "en-US",
            Variables = new Dictionary<string, string>()
        };

        var result = _sut.RenderEmail(request);

        result.PlainTextBody.Should().Contain("{{PublicCode}}");
    }

    [Fact]
    public void RenderEmail_WhenExtraVariablesProvided_IgnoresExtraVariables()
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.ReturnReminder,
            Locale = "en-US",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R003", ["ExtraKey"] = "ignored" }
        };

        var result = _sut.RenderEmail(request);

        result.PlainTextBody.Should().Contain("R003");
        result.PlainTextBody.Should().NotContain("ignored");
    }

    [Theory]
    [InlineData(NotificationTemplateKeys.PasswordResetCustomer, "en-US", "Password reset request")]
    [InlineData(NotificationTemplateKeys.PasswordResetAdmin, "en-US", "Admin password reset")]
    [InlineData(NotificationTemplateKeys.ReservationConfirmed, "en-US", "Reservation confirmed")]
    [InlineData(NotificationTemplateKeys.ReservationCancelled, "en-US", "Reservation cancelled")]
    [InlineData(NotificationTemplateKeys.PaymentReceived, "en-US", "Payment received")]
    [InlineData(NotificationTemplateKeys.DepositReleased, "en-US", "Deposit released")]
    [InlineData(NotificationTemplateKeys.PickupReminder, "en-US", "Vehicle pickup reminder")]
    [InlineData(NotificationTemplateKeys.ReturnReminder, "en-US", "Vehicle return reminder")]
    public void RenderEmail_WhenAllTemplateKeysEnUS_ReturnsExpectedSubject(string templateKey, string locale, string expectedSubject)
    {
        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "test@test.com",
            TemplateKey = templateKey,
            Locale = locale,
            Variables = new Dictionary<string, string> { ["Token"] = "tk", ["ExpiresAtUtc"] = "dt", ["PublicCode"] = "pc" }
        };

        var result = _sut.RenderEmail(request);

        result.Subject.Should().Be(expectedSubject);
    }

    [Fact]
    public void RenderSms_WhenReservationConfirmedEnUS_ReturnsCorrectBody()
    {
        var request = new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+901234567890",
            TemplateKey = NotificationTemplateKeys.ReservationConfirmed,
            Locale = "en-US",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R123" }
        };

        var result = _sut.RenderSms(request);

        result.Body.Should().Be("Your reservation has been created. Code: R123");
    }

    [Fact]
    public void RenderSms_WhenUnsupportedLocale_FallsBackToTrTR()
    {
        var request = new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+901234567890",
            TemplateKey = NotificationTemplateKeys.ReservationCancelled,
            Locale = "ja-JP",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R555" }
        };

        var result = _sut.RenderSms(request);

        result.Body.Should().Be("Rezervasyonunuz iptal edildi. Kod: R555");
    }

    [Fact]
    public void RenderSms_WhenInvalidTemplateKey_ThrowsInvalidOperationException()
    {
        var request = new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+901234567890",
            TemplateKey = "InvalidKey",
            Locale = "en-US",
            Variables = new Dictionary<string, string>()
        };

        Action act = () => _sut.RenderSms(request);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SMS template could not be resolved*");
    }

    [Fact]
    public void RenderEmail_WhenDefaultLocaleConfigured_UsesConfiguredFallback()
    {
        var sut = new NotificationTemplateService(Options.Create(new NotificationOptions
        {
            DefaultLocale = "en-US"
        }));

        var request = new QueuedEmailNotificationRequest
        {
            ToEmail = "user@example.com",
            TemplateKey = NotificationTemplateKeys.ReservationCancelled,
            Locale = "",
            Variables = new Dictionary<string, string> { ["PublicCode"] = "R100" }
        };

        var result = sut.RenderEmail(request);

        result.Subject.Should().Be("Reservation cancelled");
    }

    [Theory]
    [InlineData(NotificationTemplateKeys.ReservationConfirmed, "en-US", "Your reservation has been created. Code: {{PublicCode}}")]
    [InlineData(NotificationTemplateKeys.ReservationCancelled, "en-US", "Your reservation has been cancelled. Code: {{PublicCode}}")]
    [InlineData(NotificationTemplateKeys.PaymentReceived, "en-US", "Your payment has been received. Reservation code: {{PublicCode}}")]
    [InlineData(NotificationTemplateKeys.DepositReleased, "en-US", "Your deposit has been released. Reservation code: {{PublicCode}}")]
    [InlineData(NotificationTemplateKeys.PickupReminder, "en-US", "Reminder: Your vehicle pickup time is approaching. Code: {{PublicCode}}")]
    [InlineData(NotificationTemplateKeys.ReturnReminder, "en-US", "Reminder: Your vehicle return time is approaching. Code: {{PublicCode}}")]
    public void RenderSms_WhenAllKeys_ReturnsExpectedTemplateBody(string templateKey, string locale, string expectedTemplate)
    {
        var request = new QueuedSmsNotificationRequest
        {
            ToPhoneNumber = "+901234567890",
            TemplateKey = templateKey,
            Locale = locale,
            Variables = new Dictionary<string, string> { ["PublicCode"] = "XYZ" }
        };

        var result = _sut.RenderSms(request);

        result.Body.Should().Be(expectedTemplate.Replace("{{PublicCode}}", "XYZ"));
    }
}
