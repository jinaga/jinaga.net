using Jinaga.Facts;
using Jinaga.Pipelines;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public class Observation<TProjection>
    {
        public ObservationWithIdentity<TProjection, TIdentity> OnAdded<TIdentity>(Func<TProjection, Task<TIdentity>> added)
        {
            throw new NotImplementedException();
        }
    }
}
