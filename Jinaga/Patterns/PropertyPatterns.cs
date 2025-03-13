using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Extensions;

namespace Jinaga.Patterns
{
    public static class PropertyPatterns
    {
        public static IQueryable<TProperty> WhereCurrent<TProperty>(
            this IQueryable<TProperty> properties,
            Expression<Func<TProperty, IEnumerable<TProperty>>> nextPriorSelector)
        {
            return properties.WhereNo(nextPriorSelector);
        }
    }
}