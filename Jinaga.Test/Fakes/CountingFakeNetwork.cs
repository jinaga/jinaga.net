using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System.Collections.Immutable;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Fakes
{
    internal class CountingFakeNetwork : INetwork
    {
        private readonly FakeNetwork innerNetwork;
        public int SaveCallCount { get; private set; } = 0;

        /// <summary>
        /// How many times the client declared a specification's feeds. Two calls for
        /// specifications that reduce to the same thing is a duplicate registration.
        /// </summary>
        public int FeedsCallCount { get; private set; } = 0;

        public FactGraph UploadedGraph => innerNetwork.UploadedGraph;
        public ImmutableList<Fact> UploadedFacts => innerNetwork.UploadedFacts;

        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged
        {
            add { innerNetwork.OnAuthenticationStateChanged += value; }
            remove { innerNetwork.OnAuthenticationStateChanged -= value; }
        }

        public CountingFakeNetwork(ITestOutputHelper output)
        {
            innerNetwork = new FakeNetwork(output);
        }

        public void AddFeed(string name, object[] facts, int delay = 0)
        {
            innerNetwork.AddFeed(name, facts, delay);
        }

        public Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            FeedsCallCount++;
            return innerNetwork.Feeds(givenTuple, specification, cancellationToken);
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            return innerNetwork.FetchFeed(feed, bookmark, cancellationToken);
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            return innerNetwork.Load(factReferences, cancellationToken);
        }

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            return innerNetwork.Login(cancellationToken);
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            SaveCallCount++;
            return innerNetwork.Save(graph, cancellationToken);
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            innerNetwork.StreamFeed(feed, bookmark, cancellationToken, onResponse, onError);
        }
    }
}
