using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class VehicleGroupRepository(IApplicationDbContext dbContext) : Repository<VehicleGroup>(dbContext, dbContext.VehicleGroups), IVehicleGroupRepository
{
    protected override IQueryable<VehicleGroup> BuildListQuery()
    {
        return Entities
            .AsNoTracking()
            .OrderBy(group => group.NameTr);
    }
}
