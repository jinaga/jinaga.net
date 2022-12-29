using Jinaga.Observers;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    public abstract class FactRepository
    {
        public abstract IQueryable<TFact> OfType<TFact>();
        public abstract IQueryable<TFact> OfType<TFact>(Expression<Func<TFact, bool>> predicate);
        public abstract IObservableCollection<TProjection> Observable<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification);
        public abstract IObservableCollection<TProjection> Observable<TProjection>(IQueryable<TProjection> queryable);
    }
}
