namespace RentACar.API.Contracts.Auth;

public sealed record CustomerRegisterRequest(
    string Email,
    string Password,
    string? FullName,
    string? Phone);

public sealed record UpdateProfileRequest(
    string? FullName,
    string? Phone,
    [property: System.Text.Json.Serialization.JsonIgnore] string? IdentityNumber,
    string? Nationality,
    [property: System.Text.Json.Serialization.JsonIgnore] int? LicenseYear,
    [property: System.Text.Json.Serialization.JsonIgnore] DateOnly? BirthDate);

public sealed record CustomerProfileResponse(
    Guid Id,
    string Email,
    string FullName,
    string Phone,
    [property: System.Text.Json.Serialization.JsonIgnore] string? IdentityNumber,
    string Nationality,
    [property: System.Text.Json.Serialization.JsonIgnore] int LicenseYear,
    [property: System.Text.Json.Serialization.JsonIgnore] DateOnly? BirthDate);
