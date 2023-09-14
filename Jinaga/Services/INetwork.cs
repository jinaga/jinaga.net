using Jinaga.Facts;
using Jinaga.Projections;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface INetwork
    {
        Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken);
        Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken);
        Task<(ImmutableList<Facts.FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken);
        Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken);
        Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken);
    }
}
