using Microsoft.AspNetCore.Http;

namespace RentACar.API.Options;

public sealed class RefreshTokenCookieSettings
{
    public const string SectionName = "Auth:RefreshTokenCookie";

    public string Name { get; init; } = "__Host-rac_refresh";
    public string Path { get; init; } = "/";
    public string? Domain { get; init; }
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Strict;
    public CookieSecurePolicy SecurePolicy { get; init; } = CookieSecurePolicy.Always;
    public bool HttpOnly { get; init; } = true;
    public bool IsEssential { get; init; } = true;
}
