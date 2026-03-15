namespace RentACar.API.Contracts.Auth;

public sealed record PasswordResetRequest(string Email, string PrincipalScope);
