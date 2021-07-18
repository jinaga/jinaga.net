using System;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public class ObservationWithIdentity<TProjection, TIdentity> : Observation<TProjection>
    {
        public ObservationWithIdentity<TProjection, TIdentity> OnModified(Func<TIdentity, TProjection, Task> modify)
        {
            throw new NotImplementedException();
        }

        public ObservationWithIdentity<TProjection, TIdentity> OnRemoved(Func<TIdentity, Task> remove)
        {
            throw new NotImplementedException();
        }
    }
}