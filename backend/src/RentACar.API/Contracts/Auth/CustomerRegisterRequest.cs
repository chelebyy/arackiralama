namespace RentACar.API.Contracts.Auth;

public sealed record CustomerRegisterRequest(
    string Email,
    string Password,
    string? FullName,
    string? Phone);
