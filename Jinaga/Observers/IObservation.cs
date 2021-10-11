using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Managers;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal interface IObservation<TProjection> : IWatchContext
    {
        Task<ImmutableList<KeyValuePair<Product, Func<Task>>>> NotifyAdded(ImmutableList<ProductProjection<TProjection>> results);
    }
}