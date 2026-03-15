namespace RentACar.API.Contracts.Auth;

public sealed record CustomerLoginRequest(string Email, string Password);
