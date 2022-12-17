using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.DefaultImplementations
{
    public class LocalNetwork : INetwork
    {
        public Task<ImmutableList<string>> Feeds(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableList<string>.Empty);
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            return Task.FromResult((ImmutableList<FactReference>.Empty, bookmark));
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            return Task.FromResult(FactGraph.Empty);
        }

        public Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
