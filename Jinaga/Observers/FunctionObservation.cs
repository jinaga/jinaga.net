using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Managers;
using Jinaga.Products;

namespace Jinaga.Observers
{
    public class FunctionObservation<TProjection> : IObservation
    {
        private readonly Func<TProjection, Task<Func<Task>>> added;
        private ImmutableList<AddedHandler> addedHandlers = ImmutableList<AddedHandler>.Empty;

        public FunctionObservation(Func<TProjection, Task<Func<Task>>> added)
        {
            this.added = added;
        }

        public async Task<ImmutableList<KeyValuePair<Product, Func<Task>>>> NotifyAdded(ImmutableList<ProductProjection> results)
        {
            var removals = ImmutableList<KeyValuePair<Product, Func<Task>>>.Empty;
            foreach (var result in results)
            {
                var removal = await added((TProjection)result.Projection);
                removals = removals.Add(new KeyValuePair<Product, Func<Task>>(result.Product, removal));
            }
            return removals;
        }

        public void OnAdded(Product anchor, string parameterName, Type projectionType, Func<object, Task<Func<Task>>> added)
        {
            var handler = new AddedHandler(anchor, parameterName, projectionType, added);
            addedHandlers = addedHandlers.Add(handler);
        }
    }
}