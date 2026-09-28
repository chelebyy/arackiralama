using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using RentACar.API.Contracts.Reservations;
using RentACar.API.Services;
using RentACar.Core.Entities;
using RentACar.Core.Enums;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class GuestReservationPolicyTests
{
    [Fact]
    public void LegacySensitiveFields_AreIgnoredOnInputAndOutput()
    {
        const string json = """
            {"Customer":{"FirstName":"Synthetic","LastName":"Guest","Email":"guest@example.test","Phone":"test",
            "IdentityNumber":"PRIVATE","DriverLicenseNumber":"PRIVATE","DateOfBirth":"1990-01-01T00:00:00Z",
            "DriverLicenseIssueDate":"2010-01-01T00:00:00Z"},"Driver":{"LicenseNumber":"PRIVATE","LicenseCountry":"TR",
            "DateOfBirth":"1990-01-01T00:00:00Z","LicenseIssueDate":"2010-01-01T00:00:00Z","LicenseExpiryDate":"2030-01-01T00:00:00Z",
            "Declaration":{"AgeAtPickup":30,"LicenseYearsAtPickup":8,"LicenseValidThroughReturn":true,"DocumentsAvailableAtPickup":true}}}
            """;
        var request = JsonSerializer.Deserialize<CreateReservationRequest>(json)!;
        request.Customer.IdentityNumber.Should().BeNull();
        request.Customer.DateOfBirth.Should().BeNull();
        request.Driver!.LicenseNumber.Should().BeNullOrEmpty();
        request.Driver.Declaration!.AgeAtPickup.Should().Be(30);
        JsonSerializer.Serialize(request).Should().NotContain("PRIVATE").And.NotContain("DateOfBirth").And.NotContain("LicenseNumber");
        JsonSerializer.Serialize(new ReservationDriverDto { LicenseNumber = "PRIVATE", DateOfBirth = DateTime.UtcNow })
            .Should().NotContain("PRIVATE").And.NotContain("DateOfBirth");
        var profile = JsonSerializer.Deserialize<RentACar.API.Contracts.Auth.UpdateProfileRequest>(
            """{"IdentityNumber":"PRIVATE","BirthDate":"1990-01-01","LicenseYear":2010}""")!;
        profile.IdentityNumber.Should().BeNull();
        profile.BirthDate.Should().BeNull();
        profile.LicenseYear.Should().BeNull();
    }

    [Theory]
    [InlineData(17, 0, true, true)]
    [InlineData(30, 31, true, true)]
    [InlineData(30, 8, false, true)]
    [InlineData(30, 8, true, false)]
    public void Declaration_RejectsInvalidEligibility(int age, int years, bool valid, bool documents)
    {
        var validate = () => VehicleBookingService.ValidateDeclaration(new DriverDeclaration {
            AgeAtPickup = age, LicenseYearsAtPickup = years, LicenseValidThroughReturn = valid,
            DocumentsAvailableAtPickup = documents }, age, new ReservationBookingConditions { MinAge = 21, MinLicenseYears = 2 });
        validate.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(20, 8, 20)]
    [InlineData(30, 1, 30)]
    [InlineData(30, 8, 31)]
    public void Declaration_EnforcesQuotedAgeAndVehicleMinimums(int age, int years, int quotedAge)
    {
        var validate = () => VehicleBookingService.ValidateDeclaration(new DriverDeclaration {
            AgeAtPickup = age, LicenseYearsAtPickup = years, LicenseValidThroughReturn = true,
            DocumentsAvailableAtPickup = true }, quotedAge, new ReservationBookingConditions { MinAge = 21, MinLicenseYears = 2 });
        validate.Should().Throw<ReservationQuoteConflictException>();
    }

    [Fact]
    public void Management_FailsClosedForMissingSettingsPastCutoffAndDisallowedStates()
    {
        var reservation = new Reservation { Status = ReservationStatus.Confirmed, PickupDateTime = DateTime.UtcNow.AddDays(2),
            PricingSnapshot = new ReservationPricingSnapshotV1 { BookingConditions = new ReservationBookingConditions { PaymentAtPickup = true } } };
        var policy = new GuestManagementPolicy { AllowCancellation = true, CancellationFee = 0, CancellationNoticeMinutes = 60 };
        GuestReservationService.CanManage(reservation, null, false).Should().BeFalse();
        GuestReservationService.CanManage(reservation, policy with { CancellationFee = null }, false).Should().BeFalse();
        GuestReservationService.CanManage(reservation, policy with { CancellationNoticeMinutes = null }, false).Should().BeFalse();
        GuestReservationService.CanManage(reservation, policy, true).Should().BeFalse();
        GuestReservationService.CanManage(reservation, policy, false).Should().BeTrue();
        reservation.PickupDateTime = DateTime.UtcNow.AddMinutes(30);
        GuestReservationService.CanManage(reservation, policy, false).Should().BeFalse();
        reservation.PickupDateTime = DateTime.UtcNow.AddDays(2);
        reservation.Status = ReservationStatus.Active;
        GuestReservationService.CanManage(reservation, policy, false).Should().BeFalse();
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ru")]
    [InlineData("ar")]
    public void VerificationMail_DecryptsOnlyAtRenderingAndHasLocalizedContent(string locale)
    {
        var protection = new EphemeralDataProtectionProvider();
        const string code = "SYNTHETICCODE";
        const string expiresAtUtc = "2030-01-02T12:34:56+00:00";
        var request = new QueuedEmailNotificationRequest { ToEmail = "guest@example.test", Locale = locale,
            TemplateKey = GuestReservationService.AccessTemplate, Variables = new Dictionary<string, string> {
                ["ProtectedCode"] = protection.CreateProtector(GuestReservationService.MailPurpose).Protect(code),
                ["ExpiresAtUtc"] = expiresAtUtc } };
        JsonSerializer.Serialize(request).Should().NotContain(code);
        var rendered = GuestReservationMail.Render(request, protection);
        rendered.PlainTextBody.Should().Contain(code).And.Contain(expiresAtUtc);
        rendered.Subject.Should().NotBeNullOrWhiteSpace();
        if (locale != "en") rendered.Subject.Should().NotBe("Reservation verification code");
    }
}
