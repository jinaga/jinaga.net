using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Managers;

namespace Jinaga.Observers
{
    public interface IObservation<TProjection>
    {
        Task<ImmutableList<KeyValuePair<Product, object>>> NotifyAdded(ImmutableList<ProductProjection<TProjection>> results);
        Task NotifyRemoved(ImmutableList<object> identities);
    }
    public class ObservationWithIdentity<TProjection, TIdentity> : IObservation<TProjection>
    {
        private readonly ImmutableList<Func<TProjection, Task<object>>> onAddedHandlers;
        private readonly ImmutableList<Func<object, TProjection, Task>> onModifiedHandlers;
        private readonly ImmutableList<Func<object, Task>> onRemovedHandlers;

        public ObservationWithIdentity(
            ImmutableList<Func<TProjection, Task<object>>> onAddedHandlers,
            ImmutableList<Func<object, TProjection, Task>> onModifiedHandlers,
            ImmutableList<Func<object, Task>> onRemovedHandlers)
        {
            this.onAddedHandlers = onAddedHandlers;
            this.onModifiedHandlers = onModifiedHandlers;
            this.onRemovedHandlers = onRemovedHandlers;
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnModified(Func<TIdentity, TProjection, Task> modify)
        {
            return new ObservationWithIdentity<TProjection, TIdentity>(
                onAddedHandlers,
                onModifiedHandlers.Add((id, projection) => modify((TIdentity)id, projection)),
                onRemovedHandlers);
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnRemoved(Func<TIdentity, Task> remove)
        {
            return new ObservationWithIdentity<TProjection, TIdentity>(
                onAddedHandlers,
                onModifiedHandlers,
                onRemovedHandlers.Add((id) => remove((TIdentity)id)));
        }

        public async Task<ImmutableList<KeyValuePair<Product, object>>> NotifyAdded(ImmutableList<ProductProjection<TProjection>> results)
        {
            var identities = ImmutableList<KeyValuePair<Product, object>>.Empty;
            foreach (var onAddedHandler in onAddedHandlers)
            {
                foreach (var result in results)
                {
                    await onAddedHandler(result.Projection);
                }
            }
            return identities;
        }

        public Task NotifyRemoved(ImmutableList<object> identities)
        {
            throw new NotImplementedException();
        }
    }
}