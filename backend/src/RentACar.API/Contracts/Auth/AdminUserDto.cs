namespace RentACar.API.Contracts.Auth;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime? LastLoginAtUtc);
