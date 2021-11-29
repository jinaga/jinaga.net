using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Store.SQLite
{
    public class SQLiteStore : IStore
    {
        Task<FactGraph> IStore.Load(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<ImmutableList<Product>> IStore.Query(ImmutableList<FactReference> startReferences, Specification specification, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<ImmutableList<Fact>> IStore.Save(FactGraph graph, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
