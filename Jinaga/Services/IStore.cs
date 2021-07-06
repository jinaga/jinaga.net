using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;

namespace Jinaga.Services
{
    public interface IStore
    {
        Task<ImmutableList<Fact>> Save(FactGraph graph);
        Task<ImmutableList<Product>> Query(FactReference startReference, string initialTag, ImmutableList<Path> paths);
        Task<FactGraph> Load(ImmutableList<FactReference> references);
    }
}
