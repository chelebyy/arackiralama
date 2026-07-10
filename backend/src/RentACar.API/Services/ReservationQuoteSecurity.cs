using System.Security.Cryptography;
using System.Text;

namespace RentACar.API.Services;

public static class ReservationQuoteSecurity
{
    public static string HashSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session identifier is required.", nameof(sessionId));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId.Trim())));
    }

    public static bool SessionHashMatches(string expectedHash, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        try
        {
            var expected = Convert.FromHexString(expectedHash);
            var actual = Convert.FromHexString(HashSessionId(sessionId));
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
