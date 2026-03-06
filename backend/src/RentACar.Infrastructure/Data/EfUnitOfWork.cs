using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Data;

public sealed class EfUnitOfWork(IApplicationDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
