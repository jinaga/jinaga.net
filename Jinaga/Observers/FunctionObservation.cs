using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

        public async Task<ImmutableList<KeyValuePair<Product, Func<Task>>>> NotifyAdded(ImmutableList<ProductAnchorProjection> results)
        {
            var removals = ImmutableList<KeyValuePair<Product, Func<Task>>>.Empty;
            foreach (var result in results)
            {
                var matchingHandlers = addedHandlers
                    .Where(h => h.Anchor.Equals(result.Anchor) && h.ParameterName == result.CollectionName);
                if (matchingHandlers.Any())
                {
                    foreach (var handler in matchingHandlers)
                    {
                        var removal = await handler.Added(result.Projection);
                        if (removal != null)
                        {
                            removals = removals.Add(new KeyValuePair<Product, Func<Task>>(result.Product, removal));
                        }
                    }
                }
                else
                {
                    var removal = await added((TProjection)result.Projection);
                    removals = removals.Add(new KeyValuePair<Product, Func<Task>>(result.Product, removal));
                }
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