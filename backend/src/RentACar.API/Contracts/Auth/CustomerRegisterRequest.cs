namespace RentACar.API.Contracts.Auth;

public sealed record CustomerRegisterRequest(
    string Email,
    string Password,
    string? FullName,
    string? Phone);

public sealed record UpdateProfileRequest(
    string? FullName,
    string? Phone,
    string? IdentityNumber,
    string? Nationality,
    int? LicenseYear,
    DateOnly? BirthDate);

public sealed record CustomerProfileResponse(
    Guid Id,
    string Email,
    string FullName,
    string Phone,
    string? IdentityNumber,
    string Nationality,
    int LicenseYear,
    DateOnly? BirthDate);
