using System.Linq.Expressions;
using RentACar.Core.Entities;

namespace RentACar.Core.Specifications;

public interface ISpecification<TEntity> where TEntity : BaseEntity
{
    Expression<Func<TEntity, bool>>? Criteria { get; }
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; }
    IReadOnlyCollection<Expression<Func<TEntity, object?>>> Includes { get; }
    bool AsNoTracking { get; }
}
