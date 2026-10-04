using Jinaga.Extensions;
using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Managers;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Jinaga.Test.Managers;

public class SubscriberTest
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly Specification<Company, Office> officesInCompany = Given<Company>.Match((company, facts) =>
        from office in facts.OfType<Office>()
        where office.company == company
        select office
    );

    [Fact]
    public async Task MidStreamError_TriggersPromptReconnect()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();

        var subscriber = GivenSubscriber(network, store);

        try
        {
            // The first call to StreamFeed succeeds immediately, resolving Start().
            await subscriber.Start();
            Assert.Equal(1, network.ConnectCount);

            // Simulate a mid-stream failure on the established connection.
            network.RaiseErrorOnLatestConnection(new Exception("Simulated dropped connection"));

            // A reconnect should happen well within the ~4 minute timer, since we configured
            // a short backoff for the test.
            await network.ConnectionReached(2).WaitAsync(Patience);

            // A failure that is not a lost registration is not one the feed needs to be
            // registered again for.
            Assert.Equal(0, network.FeedsCallCount);
        }
        finally
        {
            subscriber.Stop();
        }
    }

    [Fact]
    public async Task FeedNotFound_RegistersTheFeedAgainAndResumesFromTheStoredBookmark()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();

        var subscriber = GivenSubscriber(network, store);

        try
        {
            await subscriber.Start();

            // The replicator has forgotten the feed, so reconnecting to the same hash
            // cannot succeed.
            network.LoseRegistration();
            network.RaiseErrorOnLatestConnection(new FeedNotFoundException("feed-1"));

            await network.ConnectionReached(2).WaitAsync(Patience);

            // The feed was registered exactly once, and the stream resumed where it stopped
            // rather than from the beginning.
            Assert.Equal(1, network.FeedsCallCount);
            Assert.Equal(new[] { string.Empty, "bookmark-1" }, network.RequestedBookmarks);
        }
        finally
        {
            subscriber.Stop();
        }
    }

    [Fact]
    public async Task FeedNotFoundWhileRegistering_RegistersTheFeedOnlyOnce()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();

        // Hold the registration open, so every later error arrives while it is in flight.
        var registrationGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        network.FeedsGate = registrationGate.Task;

        var subscriber = GivenSubscriber(network, store);

        try
        {
            await subscriber.Start();

            network.LoseRegistration();
            network.RaiseErrorOnLatestConnection(new FeedNotFoundException("feed-1"));
            await network.FeedsReached(1).WaitAsync(Patience);

            // Nine more backoff ticks report the same lost registration.
            for (int tick = 0; tick < 9; tick++)
            {
                network.RaiseErrorOnLatestConnection(new FeedNotFoundException("feed-1"));
            }

            registrationGate.SetResult(true);
            await network.ConnectionReached(2).WaitAsync(Patience);

            Assert.Equal(1, network.FeedsCallCount);
        }
        finally
        {
            registrationGate.TrySetResult(true);
            subscriber.Stop();
        }
    }

    [Fact]
    public async Task FactsAfterRegisteringAgain_ReachTheObserverAndAdvanceTheBookmark()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();

        var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
        var reference = collector.Serialize(new TestFact("after-registering-again"));
        network.ResponseGraph = collector.Graph;

        var observed = new TaskCompletionSource<ImmutableList<Fact>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = GivenSubscriber(network, store, (graph, facts, cancellationToken) =>
        {
            observed.TrySetResult(facts);
            return Task.CompletedTask;
        });

        try
        {
            await subscriber.Start();

            // The stream that follows the registration carries a fact. Until the feed is
            // registered again, every stream reports that it is not found, so a fact can
            // only arrive by way of a registration.
            network.ResponseReferences = ImmutableList.Create(reference);
            network.LoseRegistration();
            network.RaiseErrorOnLatestConnection(new FeedNotFoundException("feed-1"));

            var facts = await observed.Task.WaitAsync(Patience);

            Assert.Equal(new[] { reference }, facts.Select(fact => fact.Reference));
            Assert.Equal("bookmark-2", await store.LoadBookmark("feed-1"));
        }
        finally
        {
            subscriber.Stop();
        }
    }

    [Fact]
    public async Task ConcurrentAddRefAndRelease_DoNotLoseCount()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();
        var subscriber = GivenSubscriber(network, store);

        const int concurrency = 100;

        // Fire many AddRef calls concurrently. Exactly one of them should report
        // that it transitioned the ref count from 0 to 1.
        var addRefTasks = Enumerable.Range(0, concurrency)
            .Select(_ => Task.Run(() => subscriber.AddRef()))
            .ToArray();
        var addRefResults = await Task.WhenAll(addRefTasks);

        Assert.Equal(1, addRefResults.Count(wasFirst => wasFirst));

        // Now release the same number of times concurrently. Exactly one of them
        // should report that it transitioned the ref count from 1 to 0.
        var releaseTasks = Enumerable.Range(0, concurrency)
            .Select(_ => Task.Run(() => subscriber.Release()))
            .ToArray();
        var releaseResults = await Task.WhenAll(releaseTasks);

        Assert.Equal(1, releaseResults.Count(wasLast => wasLast));
    }

    private static Subscriber GivenSubscriber(
        ScriptedStreamNetwork network,
        MemoryStore store,
        Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers = null)
    {
        return new Subscriber(
            "feed-1",
            network,
            store,
            NullLogger.Instance,
            notifyObservers ?? ((graph, facts, cancellationToken) => Task.CompletedTask),
            // The owner of the feed registers it again by declaring it to the network, exactly
            // as NetworkManager does.
            cancellationToken => network.Feeds(FactReferenceTuple.Empty, officesInCompany, cancellationToken),
            reconnectInitialDelay: TimeSpan.FromMilliseconds(20),
            reconnectMaxDelay: TimeSpan.FromMilliseconds(200));
    }
}
