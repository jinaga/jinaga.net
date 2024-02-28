using System.Collections.Generic;

namespace Jinaga.Observers
{
    public interface IObservableCollection<TProjection> : IEnumerable<TProjection>
    {
        void OnAdded(OnAddedHandler<TProjection> added);
    }
}
