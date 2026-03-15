namespace RentACar.API.Contracts.Auth;

public sealed record AdminRefreshResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc);
