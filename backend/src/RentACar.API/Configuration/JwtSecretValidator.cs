using Microsoft.Extensions.Hosting;
using RentACar.API.Options;

namespace RentACar.API.Configuration;

internal static class JwtSecretValidator
{
    private const string PlaceholderSecret = "CHANGE_THIS_TO_A_32_CHAR_MINIMUM_SECRET";
    private const string DevelopmentSecret = "DevelopmentOnlySecretKeyAtLeast32Chars";

    public static void Validate(JwtOptions jwtOptions, IHostEnvironment environment)
    {
        if (jwtOptions is null)
        {
            throw new ArgumentNullException(nameof(jwtOptions));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT secret must be configured with at least 32 characters.");
        }

        if (environment.IsProduction() || environment.IsStaging())
        {
            var secret = jwtOptions.Secret.Trim();
            if (IsWeakDefaultSecret(secret))
            {
                throw new InvalidOperationException("JWT secret staging/production ortamlarında varsayılan veya placeholder değer olamaz.");
            }
        }
    }

    private static bool IsWeakDefaultSecret(string secret) =>
        secret.Equals(PlaceholderSecret, StringComparison.Ordinal) ||
        secret.Equals(DevelopmentSecret, StringComparison.Ordinal);
}
