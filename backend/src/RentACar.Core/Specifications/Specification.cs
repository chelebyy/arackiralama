using System.Linq.Expressions;
using RentACar.Core.Entities;

namespace RentACar.Core.Specifications;

public abstract class Specification<TEntity> : ISpecification<TEntity> where TEntity : BaseEntity
{
    private readonly List<Expression<Func<TEntity, object?>>> _includes = [];

    public Expression<Func<TEntity, bool>>? Criteria { get; private set; }
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; private set; }
    public IReadOnlyCollection<Expression<Func<TEntity, object?>>> Includes => _includes;
    public bool AsNoTracking { get; private set; } = true;

    protected void ApplyCriteria(Expression<Func<TEntity, bool>> criteria)
    {
        Criteria = criteria;
    }

    protected void ApplyOrderBy(Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy)
    {
        OrderBy = orderBy;
    }

    protected void AddInclude(Expression<Func<TEntity, object?>> includeExpression)
    {
        _includes.Add(includeExpression);
    }

    protected void UseTracking()
    {
        AsNoTracking = false;
    }
}
