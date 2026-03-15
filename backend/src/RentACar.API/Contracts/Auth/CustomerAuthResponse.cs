namespace RentACar.API.Contracts.Auth;

public sealed record CustomerAuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    Guid CustomerId,
    string Email,
    string FullName);
