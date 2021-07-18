using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public class Observation<TProjection>
    {
        private readonly ImmutableList<Func<TProjection, Task>> onAddedHandlers;

        public Observation() : this(ImmutableList<Func<TProjection, Task>>.Empty)
        {
        }

        public Observation(ImmutableList<Func<TProjection, Task>> onAddedHandlers)
        {
            this.onAddedHandlers = onAddedHandlers;
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnAdded<TIdentity>(Func<TProjection, Task<TIdentity>> added)
        {
            return new ObservationWithIdentity<TProjection, TIdentity>(onAddedHandlers.Add(async projection => await added(projection)));
        }

        internal async Task NotifyAdded(ImmutableList<TProjection> results)
        {
            foreach (var onAddedHandler in onAddedHandlers)
            {
                foreach (var result in results)
                {
                    await onAddedHandler(result);
                }
            }
        }
    }
}
