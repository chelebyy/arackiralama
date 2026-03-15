namespace RentACar.API.Contracts.Auth;

public sealed record CustomerRefreshResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc);
