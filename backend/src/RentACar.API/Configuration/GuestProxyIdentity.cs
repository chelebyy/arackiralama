using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RentACar.API.Configuration;

public static class GuestProxyIdentity
{
    public static string? Partition(HttpContext context, string? secret)
    {
        if (!context.Request.Path.StartsWithSegments("/api/guest/v1/reservation") || secret is not { Length: >= 32 })
            return null;
        var client = context.Request.Headers["X-Guest-Client"].ToString();
        var timestamp = context.Request.Headers["X-Guest-Timestamp"].ToString();
        var signature = context.Request.Headers["X-Guest-Signature"].ToString();
        if (client.Length != 64 || !client.All(char.IsAsciiHexDigit) || signature.Length != 64 ||
            !signature.All(char.IsAsciiHexDigit) ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return null;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (seconds < now - 60 || seconds > now + 60) return null;
        var payload = $"{timestamp}\n{client}\n{context.Request.Method}\n{context.Request.Path}";
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature))
            ? $"guest-proxy:{client.ToLowerInvariant()}" : null;
    }
}
