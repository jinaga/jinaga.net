using Jinaga.Observers;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    public abstract class FactRepository
    {
        public abstract IQueryable<TFact> OfType<TFact>()
            where TFact : class;
        public abstract IQueryable<TFact> OfType<TFact>(Expression<Func<TFact, bool>> predicate)
            where TFact : class;
        public abstract bool Any<TFact>(Expression<Func<TFact, bool>> predicate)
            where TFact : class;
        public abstract IObservableCollection<TProjection> Observable<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification)
            where TFact : class;
        public abstract IObservableCollection<TProjection> Observable<TProjection>(IQueryable<TProjection> queryable);

        public IQueryable<TSuccessor> Successors<TSuccessor>(Expression<Func<TSuccessor, bool>> predicate)
            where TSuccessor : class
        {
            return OfType(predicate);
        }
    }
}
