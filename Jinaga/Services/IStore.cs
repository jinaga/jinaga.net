using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;

namespace Jinaga.Services
{
    public interface IStore
    {
        Task<ImmutableList<Fact>> Save(FactGraph graph, System.Threading.CancellationToken cancellationToken);
        Task<ImmutableList<Product>> Query(FactReference startReference, string initialTag, ImmutableList<Path> paths, System.Threading.CancellationToken cancellationToken);
        Task<ImmutableList<Product>> QueryAll(ImmutableList<FactReference> startReferences, string initialTag, ImmutableList<Path> paths, CancellationToken cancellationToken);
        Task<FactGraph> Load(ImmutableList<FactReference> references, System.Threading.CancellationToken cancellationToken);
    }
}
