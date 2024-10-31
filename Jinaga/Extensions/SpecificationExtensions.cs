using System;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Extensions
{
    public static class SpecificationExtensions
    {
        public static IQueryable<TSuccessor> Successors<TSource, TSuccessor>(this TSource source, Expression<Func<TSuccessor, TSource>> predecessorSelector)
        {
            // The logic isn't implemented here.
            // It's in the LINQ query provider.
            throw new NotImplementedException();
        }
    }
}