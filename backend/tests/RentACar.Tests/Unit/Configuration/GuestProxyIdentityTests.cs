using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RentACar.API.Configuration;
using Xunit;

namespace RentACar.Tests.Unit.Configuration;

public sealed class GuestProxyIdentityTests
{
    public const string Secret = "test-only-guest-proxy-secret-at-least-32-characters";

    [Fact]
    public void Partition_IsStablePerClientAndDistinctAcrossClients()
    {
        var first = Signed("a");
        GuestProxyIdentity.Partition(first, Secret).Should().Be(GuestProxyIdentity.Partition(Signed("a"), Secret));
        GuestProxyIdentity.Partition(first, Secret).Should().NotBe(GuestProxyIdentity.Partition(Signed("b"), Secret));
    }

    [Theory]
    [InlineData("client")]
    [InlineData("signature")]
    [InlineData("timestamp")]
    [InlineData("path")]
    [InlineData("method")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("secret")]
    public void Partition_RejectsUntrustedOrReplayedIdentity(string change)
    {
        var context = Signed("a", change == "expired" ? -120 : change == "future" ? 120 : 0);
        if (change == "client") context.Request.Headers["X-Guest-Client"] = new string('b', 64);
        if (change == "signature") context.Request.Headers["X-Guest-Signature"] = new string('0', 64);
        if (change == "timestamp") context.Request.Headers["X-Guest-Timestamp"] = long.MaxValue.ToString();
        if (change == "path") context.Request.Path = "/api/guest/v1/reservation/verify";
        if (change == "method") context.Request.Method = "GET";
        GuestProxyIdentity.Partition(context, change == "secret" ? null : Secret).Should().BeNull();
    }

    private static DefaultHttpContext Signed(string client, int offset = 0)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/guest/v1/reservation/request";
        var partition = new string(client[0], 64);
        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offset).ToString(CultureInfo.InvariantCulture);
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes($"{timestamp}\n{partition}\nPOST\n{context.Request.Path}"));
        context.Request.Headers["X-Guest-Client"] = partition;
        context.Request.Headers["X-Guest-Timestamp"] = timestamp;
        context.Request.Headers["X-Guest-Signature"] = Convert.ToHexString(signature);
        return context;
    }
}
