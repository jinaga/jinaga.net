using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public class Observation<TProjection>
    {
        protected readonly ImmutableList<Func<TProjection, Task<object>>> onAddedHandlers;

        public Observation() : this(ImmutableList<Func<TProjection, Task<object>>>.Empty)
        {
        }

        public Observation(ImmutableList<Func<TProjection, Task<object>>> onAddedHandlers)
        {
            this.onAddedHandlers = onAddedHandlers;
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnAdded<TIdentity>(Func<TProjection, Task<TIdentity>> added) where TIdentity: struct
        {
            return new ObservationWithIdentity<TProjection, TIdentity>(
                onAddedHandlers.Add(async projection => (object)(await added(projection))),
                ImmutableList<Func<object, TProjection, Task>>.Empty,
                ImmutableList<Func<object, Task>>.Empty
            );
        }
    }
}
