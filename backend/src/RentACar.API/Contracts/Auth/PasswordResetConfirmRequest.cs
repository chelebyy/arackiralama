namespace RentACar.API.Contracts.Auth;

public sealed record PasswordResetConfirmRequest(string Token, string NewPassword, string PrincipalScope);
