using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class OfficeRepository(IApplicationDbContext dbContext) : IOfficeRepository
{
    public async Task<IReadOnlyList<Office>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Offices
            .AsNoTracking()
            .OrderBy(office => office.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Office?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Offices.FirstOrDefaultAsync(office => office.Id == id, cancellationToken);
    }

    public Task AddAsync(Office office, CancellationToken cancellationToken = default)
    {
        return dbContext.Offices.AddAsync(office, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
