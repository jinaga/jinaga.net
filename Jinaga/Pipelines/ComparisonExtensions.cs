using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public static class ComparisonExtensions
    {
        public static int SequenceHash<T>(this IEnumerable<T> sequence) where T : class
        {
            return sequence
                .Aggregate(0, (prior, obj) =>
                    prior * 37 + obj.GetHashCode());
        }

        public static int SetHash<T>(this IEnumerable<T> set) where T : class
        {
            return set
                .Aggregate(0, (prior, obj) =>
                    prior ^ obj.GetHashCode());
        }

        public static bool SetEquals<T>(this IEnumerable<T> left, IEnumerable<T> right) where T : class
        {
            return left.ToImmutableHashSet().SetEquals(right);
        }
    }
}
