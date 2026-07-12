namespace RentACar.API.Contracts.Auth;

public sealed record CustomerAccountClaimRequest(string Token, string NewPassword);

public sealed record CustomerAccountClaimInitiateRequest(string Email);