using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class GuestReservationMailTests
{
    [Fact]
    public void SharedPersistentKeyRing_DecryptsAcrossIndependentProvidersAndRestart()
    {
        var directory = Directory.CreateTempSubdirectory("guest-mail-test-");
        try
        {
            var api = DataProtectionProvider.Create(directory, b => b.SetApplicationName("RentACar.GuestReservations"));
            var request = new QueuedEmailNotificationRequest
            {
                ToEmail = "guest@example.test", Locale = "en", TemplateKey = "guest-reservation-access",
                Variables = new Dictionary<string, string>
                {
                    ["ProtectedCode"] = api.CreateProtector("GuestReservationEmail.v1").Protect("TEST-CODE"),
                    ["ExpiresAtUtc"] = DateTime.UtcNow.AddMinutes(10).ToString("O")
                }
            };
            for (var i = 0; i < 2; i++)
            {
                var worker = DataProtectionProvider.Create(directory, b => b.SetApplicationName("RentACar.GuestReservations"));
                GuestReservationMail.Render(request, worker).PlainTextBody.Should().Contain("TEST-CODE");
            }
        }
        finally { directory.Delete(true); }
    }
}
