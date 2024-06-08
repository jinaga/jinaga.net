using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public class Relation
    {
        public LambdaExpression Body { get; }

        public Relation(LambdaExpression body)
        {
            Body = body;
        }

        public static IQueryable<T> Define<T>(Expression<Func<FactRepository, IQueryable<T>>> value)
        {
            return new Relation<T>(value);
        }
    }

    public class Relation<T> : Relation, IQueryable<T>
    {
        public Relation(Expression<Func<FactRepository, IQueryable<T>>> body) : base(body)
        {
        }

        public Type ElementType => throw new NotImplementedException();

        public Expression Expression => throw new NotImplementedException();

        public IQueryProvider Provider => throw new NotImplementedException();

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}