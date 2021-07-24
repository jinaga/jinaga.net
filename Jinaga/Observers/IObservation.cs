using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Managers;

namespace Jinaga.Observers
{
    public interface IObservation<TProjection>
    {
        Task<ImmutableList<KeyValuePair<Product, object>>> NotifyAdded(ImmutableList<ProductProjection<TProjection>> results);
        Task NotifyRemoved(ImmutableList<object> identities);
    }
}