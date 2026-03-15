namespace RentACar.API.Contracts.Auth;

public sealed record AdminUserCreateRequest(
    string Email,
    string Password,
    string FullName,
    string Role);
