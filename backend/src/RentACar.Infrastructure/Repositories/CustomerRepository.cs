using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.Infrastructure.Repositories;

public sealed class CustomerRepository(IApplicationDbContext dbContext) : Repository<Customer>(dbContext, dbContext.Customers), IRepository<Customer>
{
    protected override IQueryable<Customer> BuildListQuery()
    {
        return Entities
            .AsNoTracking()
            .OrderBy(customer => customer.FullName);
    }
}
