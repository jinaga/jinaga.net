using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System.Collections.Immutable;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// A fake network that fails a configurable number of times before succeeding.
    /// Used to verify that NetworkManager retries a failed fetch/subscribe exactly once.
    /// </summary>
    internal class FlakyFakeNetwork : INetwork
    {
        private readonly FakeNetwork innerNetwork;
        private int remainingFetchFeedFailures;
        private int remainingStreamFeedFailures;

        public int FetchFeedCallCount { get; private set; } = 0;
        public int FetchFeedFailureCount { get; private set; } = 0;
        public int StreamFeedFailureCount { get; private set; } = 0;

#pragma warning disable CS0067
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        public FlakyFakeNetwork(ITestOutputHelper output, int failureCount)
        {
            innerNetwork = new FakeNetwork(output);
            remainingFetchFeedFailures = failureCount;
            remainingStreamFeedFailures = failureCount;
        }

        public void AddFeed(string name, object[] facts, int delay = 0)
        {
            innerNetwork.AddFeed(name, facts, delay);
        }

        public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            return innerNetwork.Feeds(givenTuple, specification, cancellationToken);
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            FetchFeedCallCount++;
            if (remainingFetchFeedFailures > 0)
            {
                remainingFetchFeedFailures--;
                FetchFeedFailureCount++;
                throw new InvalidOperationException("Simulated transient network failure.");
            }
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
            return innerNetwork.Save(graph, cancellationToken);
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            if (remainingStreamFeedFailures > 0)
            {
                remainingStreamFeedFailures--;
                StreamFeedFailureCount++;
                onError(new InvalidOperationException("Simulated transient network failure."));
                return;
            }
            innerNetwork.StreamFeed(feed, bookmark, cancellationToken, onResponse, onError);
        }
    }
}
