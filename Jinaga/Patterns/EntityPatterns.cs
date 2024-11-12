using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Extensions;

namespace Jinaga.Patterns
{
    public static class EntityPatterns
    {
        public static IQueryable<TEntity> WhereNotDeleted<TEntity, TDeleted>(
            this IQueryable<TEntity> entities,
            Expression<Func<TDeleted, TEntity>> deletedEntitySelector)
        {
            return entities.WhereNo(deletedEntitySelector);
        }

        public static IQueryable<TEntity> WhereNotDeletedOrRestored<TEntity, TDeleted, TRestored>(
            this IQueryable<TEntity> entities,
            Expression<Func<TDeleted, TEntity>> deletedEntitySelector,
            Expression<Func<TRestored, TDeleted>> restoredDeletedSelector)
        {
            return entities.Where(s => !s.Successors().OfType(deletedEntitySelector)
                .WhereNo(restoredDeletedSelector)
                .Any());
        }
    }
}