using System.Security.Claims;
using RentACar.Core.Enums;

namespace RentACar.API.Authentication;

public sealed record JwtPrincipalClaims(
    Guid PrincipalId,
    string Email,
    AuthPrincipalType PrincipalType,
    string Role,
    int TokenVersion,
    Guid SessionId,
    IReadOnlyCollection<Claim>? AdditionalClaims = null);
