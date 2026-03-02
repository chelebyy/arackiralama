namespace RentACar.API.Contracts.Auth;

public sealed record AdminLoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string Role,
    string FullName,
    string Email);
