using System;
using System.Linq.Expressions;
using System.Linq;
using Jinaga.Observers;

namespace Jinaga.Repository
{
    public class FactRepository
    {
        private readonly JinagaQueryProvider queryProvider = new JinagaQueryProvider();

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

        public IObservableCollection<TProjection> All<TFact, TProjection>(
            TFact start,
            SpecificationOld<TFact, TProjection> specification)
        {
            throw new NotImplementedException();
        }

        public IObservableCollection<TProjection> All<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification)
        {
            throw new NotImplementedException();
        }
    }
}
