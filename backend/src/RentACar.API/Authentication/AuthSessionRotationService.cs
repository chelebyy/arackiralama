using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Authentication;

public sealed class AuthSessionRotationService(IApplicationDbContext dbContext) : IAuthSessionRotationService
{
    public async Task<bool> TryRotateAsync(Guid currentSessionId, AuthSession replacement, CancellationToken cancellationToken)
    {
        if (!replacement.TokenVersion.HasValue)
        {
            return false;
        }

        if (dbContext is not DbContext relationalContext || !relationalContext.Database.IsRelational())
        {
            throw new InvalidOperationException("Session rotation requires a relational database.");
        }

        await using var transaction = await relationalContext.Database.BeginTransactionAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;
        var consumed = await dbContext.AuthSessions
            .Where(session => session.Id == currentSessionId
                && session.PrincipalType == replacement.PrincipalType
                && session.PrincipalId == replacement.PrincipalId
                && session.TokenVersion == replacement.TokenVersion
                && !session.RevokedAtUtc.HasValue
                && !session.ReplacedBySessionId.HasValue
                && session.RefreshTokenExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.RevokedAtUtc, utcNow)
                .SetProperty(session => session.ReplacedBySessionId, replacement.Id)
                .SetProperty(session => session.LastSeenAtUtc, utcNow), cancellationToken);

        if (consumed != 1)
        {
            return false;
        }

        dbContext.AuthSessions.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
