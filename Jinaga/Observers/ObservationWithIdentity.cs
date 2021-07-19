using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public class ObservationWithIdentity<TProjection, TIdentity> : Observation<TProjection>
    {
        public ObservationWithIdentity(ImmutableList<Func<TProjection, Task>> onAddedHandlers) :
            base(onAddedHandlers)
        {
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnModified(Func<TIdentity, TProjection, Task> modify)
        {
            return this;
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnRemoved(Func<TIdentity, Task> remove)
        {
            return this;
        }
    }
}