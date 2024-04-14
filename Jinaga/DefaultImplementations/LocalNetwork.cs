using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.DefaultImplementations
{
    public class LocalNetwork : INetwork
    {
        public ImmutableList<FactReference> SavedFactReferences { get; private set; } = ImmutableList<FactReference>.Empty;

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableList<string>.Empty);
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            return Task.FromResult((ImmutableList<FactReference>.Empty, bookmark));
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            onResponse(ImmutableList<FactReference>.Empty, bookmark);
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            return Task.FromResult(FactGraph.Empty);
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            SavedFactReferences = SavedFactReferences.AddRange(graph.FactReferences);
            return Task.CompletedTask;
        }
    }
}
