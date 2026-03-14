using System.Security.Cryptography;
using System.Text;

namespace RentACar.Infrastructure.Services.Payments;

internal static class PaymentSignatureHelper
{
    internal static string CreateSha256Signature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    internal static string NormalizeSignature(string signature) =>
        signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature["sha256=".Length..]
            : signature;

    internal static bool IsValidSignature(string payload, string secret, string signature)
    {
        var expected = CreateSha256Signature(payload, secret);
        var normalized = NormalizeSignature(signature).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(normalized));
    }
}
