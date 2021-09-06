using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Managers;

namespace Jinaga.Observers
{
    public class FunctionObservation<TProjection> : IObservation<TProjection>
    {
        private readonly Func<TProjection, Task<Func<Task>>> added;

        public FunctionObservation(Func<TProjection, Task<Func<Task>>> added)
        {
            this.added = added;
        }

        public async Task<ImmutableList<KeyValuePair<Product, object>>> NotifyAdded(ImmutableList<ProductProjection<TProjection>> results)
        {
            var removals = ImmutableList<KeyValuePair<Product, object>>.Empty;
            foreach (var result in results)
            {
                var removal = await added(result.Projection);
                removals = removals.Add(new KeyValuePair<Product, object>(result.Product, removal));
            }
            return removals;
        }

        public async Task NotifyRemoved(ImmutableList<object> removals)
        {
            foreach (var removal in removals.OfType<Func<Task>>())
            {
                await removal();
            }
        }
    }
}