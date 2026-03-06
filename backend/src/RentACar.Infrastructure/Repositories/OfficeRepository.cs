using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class OfficeRepository(IApplicationDbContext dbContext) : Repository<Office>(dbContext, dbContext.Offices), IOfficeRepository
{
    protected override IQueryable<Office> BuildListQuery()
    {
        return Entities
            .AsNoTracking()
            .OrderBy(office => office.Name);
    }
}
