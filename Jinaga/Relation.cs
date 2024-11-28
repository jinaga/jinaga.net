using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public static class Relation
    {
        public static Relation<T> Define<T>(Expression<Func<FactRepository, IQueryable<T>>> lambda)
        {
            return new Relation<T>(lambda);
        }

        public static Relation<T> Define<T>(Expression<Func<IQueryable<T>>> lambda)
        {
            return new Relation<T>(lambda);
        }
    }

    public class Relation<T> : IQueryable<T>
    {
        private readonly LambdaExpression lambda;

        public Relation(Expression<Func<FactRepository, IQueryable<T>>> lambda)
        {
            this.lambda = lambda;
        }

        public Relation(Expression<Func<IQueryable<T>>> lambda)
        {
            this.lambda = lambda;
        }

        public Type ElementType => typeof(T);

        public Expression Expression => lambda.Body;

        public IQueryProvider Provider => throw new NotImplementedException();

        public IEnumerator<T> GetEnumerator()
        {
            return Enumerable.Empty<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}