using System.Security.Claims;

namespace RentACar.API.Authentication;

public interface IAccessTokenSessionValidator
{
    Task<AccessTokenSessionValidationFailure> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
