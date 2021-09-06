using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    public interface IObservableCollection<TProjection> : IEnumerable<TProjection>
    {
        void OnAdded(Func<TProjection, Task<Func<Task>>> added);
    }
}
