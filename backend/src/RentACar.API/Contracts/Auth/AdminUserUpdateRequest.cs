namespace RentACar.API.Contracts.Auth;

public sealed record AdminUserUpdateRequest(
    string Email,
    string FullName,
    string Role);
