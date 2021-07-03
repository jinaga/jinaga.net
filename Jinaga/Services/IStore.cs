using System.Collections.Immutable;
using System.Threading.Tasks;
using Jinaga.Facts;

namespace Jinaga.Services
{
    public interface IStore
    {
        Task<ImmutableList<Fact>> Save(ImmutableList<Fact> newFacts);
    }
}
