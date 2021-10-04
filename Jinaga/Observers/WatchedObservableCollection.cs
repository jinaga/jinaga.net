using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal static class WatchedObservableCollection
    {
        public static object Create(Type elementType, IWatchContext context)
        {
            var type = typeof(WatchedObservableCollection<>).MakeGenericType(elementType);
            return Activator.CreateInstance(type, context);
        }
    }
    internal class WatchedObservableCollection<TProjection> : IObservableCollection<TProjection>
    {
        private readonly IWatchContext context;

        public WatchedObservableCollection(IWatchContext context)
        {
            this.context = context;
        }

        public IEnumerator<TProjection> GetEnumerator()
        {
            throw new InvalidOperationException("You cannot enumerate a collection after j.Watch.");
        }

        public void OnAdded(Func<TProjection, Task<Func<Task>>> added)
        {
            // TODO: Use the context to route elements of this collection
            // to the added function.
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new InvalidOperationException("You cannot enumerate a collection after j.Watch.");
        }
    }
}
