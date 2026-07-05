using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Jinaga.Test.Managers;

public class SubscriberTest
{
    [Fact]
    public async Task MidStreamError_TriggersPromptReconnect()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();

        var subscriber = new Subscriber(
            "feed-1",
            network,
            store,
            NullLogger.Instance,
            (graph, facts, cancellationToken) => Task.CompletedTask,
            reconnectInitialDelay: TimeSpan.FromMilliseconds(20),
            reconnectMaxDelay: TimeSpan.FromMilliseconds(200));

        try
        {
            // The first call to StreamFeed succeeds immediately, resolving Start().
            await subscriber.Start();
            Assert.Equal(1, network.ConnectCount);

            // Simulate a mid-stream failure on the established connection.
            network.RaiseErrorOnLatestConnection(new Exception("Simulated dropped connection"));

            // A reconnect should happen well within the ~4 minute timer, since we configured
            // a short backoff for the test. Poll for a bounded amount of time.
            var reconnected = await WaitUntil(() => network.ConnectCount >= 2, TimeSpan.FromSeconds(5));

            Assert.True(reconnected, "Expected a reconnect attempt shortly after a mid-stream error.");
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
        var subscriber = new Subscriber(
            "feed-1",
            network,
            store,
            NullLogger.Instance,
            (graph, facts, cancellationToken) => Task.CompletedTask);

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

    private static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }
            await Task.Delay(20);
        }
        return condition();
    }

    /// <summary>
    /// A fake network whose StreamFeed connection can be told to fail after it has
    /// already delivered its initial response, so tests can simulate a mid-stream drop.
    /// </summary>
    private class ScriptedStreamNetwork : INetwork
    {
        private readonly ConcurrentQueue<Action<Exception>> activeErrorCallbacks = new();

        public int ConnectCount => connectCount;
        private int connectCount = 0;

#pragma warning disable CS0067
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        public void RaiseErrorOnLatestConnection(Exception ex)
        {
            if (activeErrorCallbacks.TryDequeue(out var onError))
            {
                onError(ex);
            }
        }

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
            Interlocked.Increment(ref connectCount);
            activeErrorCallbacks.Enqueue(onError);

            // Deliver an initial (empty) response immediately so Start() resolves.
            _ = onResponse(ImmutableList<FactReference>.Empty, "bookmark-" + connectCount);
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            return Task.FromResult(FactGraph.Empty);
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
