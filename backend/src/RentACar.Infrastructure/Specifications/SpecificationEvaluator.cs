using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Specifications;

namespace RentACar.Infrastructure.Specifications;

internal static class SpecificationEvaluator<TEntity> where TEntity : BaseEntity
{
    public static IQueryable<TEntity> GetQuery(IQueryable<TEntity> inputQuery, ISpecification<TEntity> specification)
    {
        var query = inputQuery;

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        query = specification.Includes.Aggregate(query, static (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
        {
            query = specification.OrderBy(query);
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query;
    }
}
