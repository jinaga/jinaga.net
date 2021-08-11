using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    class JinagaQueryable<TFact> : IQueryable<TFact>
    {
        private readonly JinagaQueryProvider queryProvider;
        private readonly Expression expression;

        public JinagaQueryable(JinagaQueryProvider queryProvider, Expression expression)
        {
            this.queryProvider = queryProvider;
            this.expression = expression;
        }

        public Type ElementType => throw new NotImplementedException();

        public Expression Expression => expression;

        public IQueryProvider Provider => queryProvider;

        public IEnumerator<TFact> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}