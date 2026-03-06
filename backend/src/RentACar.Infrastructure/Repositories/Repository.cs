using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;
using RentACar.Core.Specifications;
using RentACar.Infrastructure.Specifications;

namespace RentACar.Infrastructure.Repositories;

public abstract class Repository<TEntity>(IApplicationDbContext dbContext, DbSet<TEntity> entities) : IRepository<TEntity>
    where TEntity : BaseEntity
{
    protected IApplicationDbContext DbContext => dbContext;
    protected DbSet<TEntity> Entities => entities;

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await BuildListQuery().ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator<TEntity>.GetQuery(Entities.AsQueryable(), specification).ToListAsync(cancellationToken);
    }

    public virtual Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Entities.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return Entities.AddAsync(entity, cancellationToken).AsTask();
    }

    public virtual void Remove(TEntity entity)
    {
        Entities.Remove(entity);
    }

    protected virtual IQueryable<TEntity> BuildListQuery()
    {
        return Entities.AsNoTracking();
    }
}
