using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
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

        public Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var feeds = ImmutableList<string>.Empty;
            return Task.FromResult((feeds, AuthorizeAll(feeds)));
        }

        /// <summary>
        /// Reports every feed this network returns as authorized. There is no replicator
        /// here to deny one, and deriving the decisions from the feeds keeps that true of
        /// whatever this network returns.
        /// </summary>
        private static ImmutableList<FeedDecision> AuthorizeAll(ImmutableList<string> feeds)
        {
            return feeds
                .Select(feed => new FeedDecision
                {
                    Feed = feed,
                    Decision = DistributionDecision.Authorized,
                    Reason = "Served from local storage."
                })
                .ToImmutableList();
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
