using System;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Extensions
{
    public static class SpecificationExtensions
    {
        public static SuccessorQuery<TSource> Successors<TSource>(this TSource source)
        {
            return new SuccessorQuery<TSource>(source);
        }
    }

    public class SuccessorQuery<TSource>
    {
        private readonly TSource source;

        public SuccessorQuery(TSource source)
        {
            this.source = source;
        }

        public IQueryable<TSuccessor> OfType<TSuccessor>(Expression<Func<TSuccessor, TSource>> predecessorSelector)
        {
            // The logic isn't implemented here.
            // It's in the LINQ query provider.
            throw new NotImplementedException();
        }

        public bool Any<TSuccessor>(Expression<Func<TSuccessor, TSource>> predecessorSelector)
        {
            // The logic isn't implemented here.
            // It's in the LINQ query provider.
            throw new NotImplementedException();
        }

        public bool No<TSuccessor>(Expression<Func<TSuccessor, TSource>> predecessorSelector)
        {
            // The logic isn't implemented here.
            // It's in the LINQ query provider.
            throw new NotImplementedException();
        }
    }
}