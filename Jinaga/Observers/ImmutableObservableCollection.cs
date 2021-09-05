using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal static class ImmutableObservableCollection
    {
        public static object Create(Type elementType, IEnumerable<object> elements)
        {
            var method = typeof(ImmutableObservableCollection)
                .GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(elementType);
            return method.Invoke(null, new [] { elements });
        }

        private static ImmutableObservableCollection<TProjection> Create<TProjection>(IEnumerable<object> elements)
        {
            return new ImmutableObservableCollection<TProjection>(
                elements.OfType<TProjection>().ToImmutableList()
            );
        }
    }
    public class ImmutableObservableCollection<TProjection> : IObservableCollection<TProjection>
    {
        private readonly ImmutableList<TProjection> projections;

        public ImmutableObservableCollection(ImmutableList<TProjection> projections)
        {
            this.projections = projections;
        }

        public IEnumerator<TProjection> GetEnumerator()
        {
            return projections.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return projections.GetEnumerator();
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnAdded<TIdentity>(Func<TProjection, Task<TIdentity>> added) where TIdentity : struct
        {
            throw new InvalidOperationException("You cannot receive notification from a collection after j.Query.");
        }
    }
}
