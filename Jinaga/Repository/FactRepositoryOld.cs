using System;
using System.Linq.Expressions;
using System.Linq;
using Jinaga.Observers;

namespace Jinaga.Repository
{
    public class FactRepositoryOld
    {
        private readonly JinagaQueryProviderOld queryProvider = new JinagaQueryProviderOld();

        public IQueryable<TFact> OfType<TFact>()
        {
            var expression = Expression.Call(
                Expression.Constant(this),
                GetType().GetMethods()
                    .Single(method => method.Name == nameof(OfType) && method.GetParameters().Count() == 0)
                    .MakeGenericMethod(typeof(TFact))
            );
            return new JinagaQueryableOld<TFact>(queryProvider, expression);
        }

        public IQueryable<TFact> OfType<TFact>(Expression<Func<TFact, bool>> predicate)
        {
            return this.OfType<TFact>().Where(predicate);
        }

        public IObservableCollection<TProjection> Observable<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification)
        {
            throw new NotImplementedException();
        }

        public IObservableCollection<TProjection> Observable<TProjection>(IQueryable<TProjection> queryable)
        {
            throw new NotImplementedException();
        }
    }
}
