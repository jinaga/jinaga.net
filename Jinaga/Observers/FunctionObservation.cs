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
        private readonly Product initialAnchor;
        private readonly Func<TProjection, Task<Func<Task>>> added;
        private ImmutableList<AddedHandler> addedHandlers = ImmutableList<AddedHandler>.Empty;

        public FunctionObservation(Product initialAnchor, Func<TProjection, Task<Func<Task>>> added)
        {
            this.initialAnchor = initialAnchor;
            this.added = added;
        }

        public async Task<ImmutableList<KeyValuePair<Product, Func<Task>>>> NotifyAdded(ImmutableList<ProductAnchorProjection> results)
        {
            var removals = ImmutableList<KeyValuePair<Product, Func<Task>>>.Empty;
            foreach (var result in results)
            {
                var newRemovals = await InvokeAddedHandler(result.Anchor, result.CollectionName, result.Projection, result.Product);
                removals = removals.AddRange(newRemovals);
            }
            return removals;
        }

        public void OnAdded(Product anchor, string collectionName, Func<object, Task<Func<Task>>> added)
        {
            var handler = new AddedHandler(anchor, collectionName, "", added);
            addedHandlers = addedHandlers.Add(handler);
        }

        private async Task<ImmutableList<KeyValuePair<Product, Func<Task>>>> InvokeAddedHandler(Product anchor, string collectionName, object projection, Product product)
        {
            var removals = ImmutableList<KeyValuePair<Product, Func<Task>>>.Empty;
            var matchingHandlers = addedHandlers
                .Where(h => h.Anchor.Equals(anchor) && h.CollectionName == collectionName);
            if (matchingHandlers.Any())
            {
                foreach (var handler in matchingHandlers)
                {
                    var removal = await handler.Added(projection);
                    if (removal != null)
                    {
                        removals = removals.Add(new KeyValuePair<Product, Func<Task>>(product.GetAnchor(), removal));
                    }
                }
            }
            else if (initialAnchor.Equals(anchor))
            {
                var removal = await added((TProjection)projection);
                removals = removals.Add(new KeyValuePair<Product, Func<Task>>(product.GetAnchor(), removal));
            }

            return removals;
        }
    }
}