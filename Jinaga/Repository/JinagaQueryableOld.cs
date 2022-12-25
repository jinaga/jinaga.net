using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    class JinagaQueryableOld<TFact> : IQueryable<TFact>
    {
        private readonly JinagaQueryProviderOld queryProvider;
        private readonly Expression expression;

        public JinagaQueryableOld(JinagaQueryProviderOld queryProvider, Expression expression)
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