using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.DefaultImplementations
{
    public class LocalNetwork : INetwork
    {
        public ImmutableList<FactReference> SavedFactReferences => UploadedGraph.FactReferences;
        public FactGraph UploadedGraph { get; private set; } = FactGraph.Empty;

#pragma warning disable CS0067
        public event INetwork.AuthenticationStateChanged? OnAuthenticationStateChanged;
#pragma warning restore CS0067

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
            UploadedGraph = UploadedGraph.AddGraph(graph);
            return Task.CompletedTask;
        }
    }
}
