using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;
using RentACar.Infrastructure.Services.Notifications;
using Xunit;

namespace RentACar.Tests.Unit.Services;

public sealed class GuestSmtpDeliveryTests
{
    [Fact]
    public async Task VerificationEmail_ReachesLoopbackSmtpSinkWithoutExternalDelivery()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = CaptureAsync(listener, timeout.Token);
        var protection = new EphemeralDataProtectionProvider();
        var message = GuestReservationMail.Render(new QueuedEmailNotificationRequest
        {
            ToEmail = "synthetic@example.test", Locale = "en", TemplateKey = "guest-reservation-access",
            Variables = new Dictionary<string, string>
            {
                ["ProtectedCode"] = protection.CreateProtector("GuestReservationEmail.v1").Protect("SYNTHETICCODE"),
                ["ExpiresAtUtc"] = "2030-01-02T12:34:56+00:00"
            }
        }, protection);
        message.PlainTextBody.Should().Contain("2030-01-02T12:34:56+00:00");
        var provider = new SmtpEmailProvider(Options.Create(new NotificationOptions
        {
            Email = new EmailNotificationOptions
            {
                Enabled = true, FromEmail = "no-reply@example.test",
                Smtp = new SmtpOptions { Host = "127.0.0.1", Port = port, EnableSsl = false }
            }
        }), NullLogger<SmtpEmailProvider>.Instance);
        var result = await provider.SendAsync(message, timeout.Token);
        result.Success.Should().BeTrue();
        var wire = await received;
        wire.Should().Contain("synthetic@example.test").And.Contain("SYNTHETICCODE")
            .And.Contain("Reservation verification code").And.NotContain("ProtectedCode");
    }

    private static async Task<string> CaptureAsync(TcpListener listener, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 localhost synthetic test sink");
        var message = new StringBuilder();
        var data = false;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (data && line != ".") { message.AppendLine(line); continue; }
            if (data)
            {
                await writer.WriteLineAsync("250 accepted locally");
                return message.ToString();
            }
            if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                data = true;
                await writer.WriteLineAsync("354 end with a dot");
            }
            else await writer.WriteLineAsync("250 localhost");
        }
        throw new InvalidOperationException("SMTP message was not received.");
    }
}
