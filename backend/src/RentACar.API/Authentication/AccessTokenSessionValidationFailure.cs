namespace RentACar.API.Authentication;

public enum AccessTokenSessionValidationFailure
{
    None = 0,
    MissingRequiredClaims,
    InvalidClaimFormat,
    SessionNotFound,
    SessionRevoked,
    SessionExpired,
    PrincipalNotFound,
    TokenVersionMismatch
}
