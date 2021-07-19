using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;

namespace Jinaga.Observers
{
    interface IObserver
    {
        Task FactsAdded(ImmutableList<Fact> added, FactGraph graph, CancellationToken cancellationToken);
    }
}