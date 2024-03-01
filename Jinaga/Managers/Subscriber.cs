using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Services;
using Microsoft.Extensions.Logging;

namespace Jinaga.Managers
{
    class Subscriber
    {
        private string feed;
        private INetwork network;
        private IStore store;
        private readonly ILogger logger;
        private Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers;

        private int refCount = 0;
        private string bookmark = string.Empty;
        private bool resolved = false;
        private TaskCompletionSource<bool>? taskCompletionSource;
        private CancellationTokenSource? cancellationTokenSource;
        private Timer? timer;

        public Subscriber(string feed, INetwork network, IStore store, ILogger logger, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers)
        {
            this.feed = feed;
            this.network = network;
            this.store = store;
            this.logger = logger;
            this.notifyObservers = notifyObservers;
        }

        public bool AddRef()
        {
            refCount++;
            return refCount == 1;
        }

        public bool Release()
        {
            refCount--;
            return refCount == 0;
        }

        public async Task Start()
        {
            bookmark = await store.LoadBookmark(feed);
            resolved = false;

            taskCompletionSource = new TaskCompletionSource<bool>();

            // Initialize the connection immediately.
            // Refresh the connection every 4 minutes.
            timer = new Timer(_ =>
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource = new CancellationTokenSource();
                ConnectToFeed(taskCompletionSource, cancellationTokenSource.Token);
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(4));

            // Wait for the connection to be established.
            await taskCompletionSource.Task;
        }

        public void Stop()
        {
            timer?.Dispose();
            timer = null;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = null;
        }

        private void ConnectToFeed(TaskCompletionSource<bool> taskCompletionSource, CancellationToken cancellationToken)
        {
            network.StreamFeed(feed, bookmark, cancellationToken, async (factReferences, newBookmark) =>
            {
                logger.LogInformation("Subscribe received {count} facts", factReferences.Count);
                var factGraph = await network.Load(factReferences, cancellationToken);
                var facts = factReferences.Select(reference => factGraph.GetFact(reference)).ToImmutableList();
                await store.Save(factGraph, cancellationToken);
                await store.SaveBookmark(feed, newBookmark);
                bookmark = newBookmark;
                await notifyObservers(factGraph, facts, cancellationToken);
                if (!resolved)
                {
                    resolved = true;
                    taskCompletionSource.SetResult(true);
                }
            }, ex =>
            {
                if (!resolved)
                {
                    resolved = true;
                    taskCompletionSource.SetException(ex);
                }
            });
        }
    }
}