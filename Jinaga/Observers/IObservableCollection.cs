using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public interface IObservableCollection<TProjection> : IEnumerable<TProjection>
    {
        ObservationWithIdentity<TProjection, TIdentity> OnAdded<TIdentity>(Func<TProjection, Task<TIdentity>> added) where TIdentity : struct;
    }
}
