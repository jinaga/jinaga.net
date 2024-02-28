using Jinaga.Facts;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Jinaga.Observers
{
    internal static class WatchedObservableCollection
    {
        public static object Create(Type elementType, FactReferenceTuple anchor, string path, IWatchContext context)
        {
            var type = typeof(WatchedObservableCollection<>).MakeGenericType(elementType);
            return Activator.CreateInstance(type, anchor, path, context);
        }
    }
    internal class WatchedObservableCollection<TProjection> : IObservableCollection<TProjection>
    {
        private readonly FactReferenceTuple anchor;
        private readonly string path;
        private readonly IWatchContext context;

        public WatchedObservableCollection(FactReferenceTuple anchor, string path, IWatchContext context)
        {
            this.anchor = anchor;
            this.path = path;
            this.context = context;
        }

        public IEnumerator<TProjection> GetEnumerator()
        {
            throw new InvalidOperationException("You cannot enumerate a collection after j.Watch.");
        }

        public void OnAdded(OnAddedHandler<TProjection> added)
        {
            context.OnAdded(anchor, path, added);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new InvalidOperationException("You cannot enumerate a collection after j.Watch.");
        }
    }
}
