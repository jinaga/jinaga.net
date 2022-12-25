using System;
using System.Linq.Expressions;
using System.Linq;
using Jinaga.Observers;

namespace Jinaga.Repository
{
    public class FactRepository
    {
        private readonly JinagaQueryProvider queryProvider = new JinagaQueryProvider();
        private SpecificationProcessor specificationProcessor;

        internal FactRepository(SpecificationProcessor specificationProcessor)
        {
            this.specificationProcessor = specificationProcessor;
        }

        public IQueryable<TFact> OfType<TFact>()
        {
            var expression = Expression.Call(
                Expression.Constant(this),
                GetType().GetMethods()
                    .Single(method => method.Name == nameof(OfType) && method.GetParameters().Count() == 0)
                    .MakeGenericMethod(typeof(TFact))
            );
            return new JinagaQueryable<TFact>(queryProvider, expression);
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
