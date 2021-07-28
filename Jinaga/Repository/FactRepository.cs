using System.Linq.Expressions;
using System.Linq;

namespace Jinaga.Repository
{
    public class FactRepository
    {
        private readonly JinagaQueryProvider queryProvider = new JinagaQueryProvider();

        public IQueryable<TFact> OfType<TFact>()
        {
            var expression = Expression.Call(
                Expression.Constant(this),
                GetType().GetMethod(nameof(OfType)).MakeGenericMethod(typeof(TFact))
            );
            return new JinagaQueryable<TFact>(queryProvider, expression);
        }
    }
}
