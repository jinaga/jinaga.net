using System.Linq.Expressions;
using System;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;

namespace Jinaga.Repository
{
    public class FactRepository
    {
        private readonly RepositoryQueryProvider queryProvider = new RepositoryQueryProvider();

        public IQueryable<TFact> OfType<TFact>()
        {
            var expression = Expression.Call(
                Expression.Constant(this),
                GetType().GetMethod(nameof(OfType)).MakeGenericMethod(typeof(TFact))
            );
            return new RepositoryQueryable<TFact>(queryProvider, expression);
        }
    }
}
