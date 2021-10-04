using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal static class WatchedObservableCollection
    {
        public static object Create(Type elementType, Product anchor, string parameterName, IWatchContext context)
        {
            var type = typeof(WatchedObservableCollection<>).MakeGenericType(elementType);
            return Activator.CreateInstance(type, anchor, parameterName, context);
        }
    }
    internal class WatchedObservableCollection<TProjection> : IObservableCollection<TProjection>
    {
        private readonly Product anchor;
        private readonly string parameterName;
        private readonly IWatchContext context;

        public WatchedObservableCollection(Product anchor, string parameterName, IWatchContext context)
        {
            this.anchor = anchor;
            this.parameterName = parameterName;
            this.context = context;
        }

        public IEnumerator<TProjection> GetEnumerator()
        {
            throw new InvalidOperationException("You cannot enumerate a collection after j.Watch.");
        }

        public void OnAdded(Func<TProjection, Task<Func<Task>>> added)
        {
            context.OnAdded(anchor, parameterName, typeof(TProjection),
                projection => added((TProjection)projection));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new InvalidOperationException("You cannot enumerate a collection after j.Watch.");
        }
    }
}
