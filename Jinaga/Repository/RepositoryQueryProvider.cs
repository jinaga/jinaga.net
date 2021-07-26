using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    class RepositoryQueryProvider : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            throw new System.NotImplementedException();
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new RepositoryQueryable<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            throw new System.NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            throw new System.NotImplementedException();
        }
    }
}