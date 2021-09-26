using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Managers;
using Jinaga.Products;

namespace Jinaga.Observers
{
    public interface IObservation<TProjection>
    {
        Task<ImmutableList<KeyValuePair<Product, object>>> NotifyAdded(ImmutableList<ProductProjection<TProjection>> results);
        Task NotifyRemoved(ImmutableList<object> identities);
    }
}