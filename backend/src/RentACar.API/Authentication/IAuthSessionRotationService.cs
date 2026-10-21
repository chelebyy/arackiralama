using RentACar.Core.Entities;

namespace RentACar.API.Authentication;

public interface IAuthSessionRotationService
{
    Task<bool> TryRotateAsync(Guid currentSessionId, AuthSession replacement, CancellationToken cancellationToken);
}
