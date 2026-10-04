using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Http;
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
        private readonly Func<CancellationToken, Task>? registerFeedAgain;

        private int refCount = 0;
        private string bookmark = string.Empty;
        private bool resolved = false;
        private TaskCompletionSource<bool>? taskCompletionSource;
        private CancellationTokenSource? cancellationTokenSource;
        private Timer? timer;
        private int reconnectAttempt = 0;
        private int registering = 0;

        private readonly TimeSpan reconnectInitialDelay;
        private readonly TimeSpan reconnectMaxDelay;

        public Subscriber(string feed, INetwork network, IStore store, ILogger logger, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers,
            Func<CancellationToken, Task>? registerFeedAgain = null,
            TimeSpan? reconnectInitialDelay = null, TimeSpan? reconnectMaxDelay = null)
        {
            this.feed = feed;
            this.network = network;
            this.store = store;
            this.logger = logger;
            this.notifyObservers = notifyObservers;
            this.registerFeedAgain = registerFeedAgain;
            this.reconnectInitialDelay = reconnectInitialDelay ?? TimeSpan.FromSeconds(1);
            this.reconnectMaxDelay = reconnectMaxDelay ?? TimeSpan.FromSeconds(30);
        }

        public bool AddRef()
        {
            return Interlocked.Increment(ref refCount) == 1;
        }

        public bool Release()
        {
            return Interlocked.Decrement(ref refCount) == 0;
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
                if (cancellationTokenSource != null)
                {
                    logger.LogInformation("Refreshing connection to feed");
                }
                try
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource = new CancellationTokenSource();
                    ConnectToFeed(taskCompletionSource, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error connecting to feed");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(4));

            // Wait for the connection to be established.
            await taskCompletionSource.Task.ConfigureAwait(false);
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

                // A successful response means the connection is healthy again.
                Interlocked.Exchange(ref reconnectAttempt, 0);

                // Load the facts that I don't already have.
                var knownFactReferences = await store.ListKnown(factReferences).ConfigureAwait(false);
                var unknownFactReferences = factReferences.RemoveRange(knownFactReferences);
                if (unknownFactReferences.Any())
                {
                    var factGraph = await network.Load(factReferences, cancellationToken);
                    var facts = factReferences.Select(reference => factGraph.GetFact(reference)).ToImmutableList();
                    await store.Save(factGraph, false, cancellationToken).ConfigureAwait(false);
                    await store.SaveBookmark(feed, newBookmark).ConfigureAwait(false);
                    bookmark = newBookmark;
                    await notifyObservers(factGraph, facts, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await store.SaveBookmark(feed, newBookmark).ConfigureAwait(false);
                    bookmark = newBookmark;
                }

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
                else if (ex is FeedNotFoundException && registerFeedAgain != null)
                {
                    // The replicator no longer holds this feed, so reconnecting to the same
                    // hash cannot succeed however long we back off. Ask the owner of the feed
                    // to declare it again, which restores the same hash.
                    logger.LogWarning(ex, "The replicator does not know this feed. Registering it again.");
                    RegisterAgainAndReconnect(taskCompletionSource, cancellationToken);
                }
                else
                {
                    // The initial connection already succeeded, so this is a mid-stream failure
                    // (e.g. a dropped connection). Rather than waiting for the ~4 minute refresh
                    // timer, reconnect promptly with an exponential backoff.
                    logger.LogWarning(ex, "Error on feed stream after connection established. Scheduling reconnect.");
                    ScheduleReconnect(taskCompletionSource, cancellationToken);
                }
            });
        }

        private void RegisterAgainAndReconnect(TaskCompletionSource<bool> taskCompletionSource, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Every backoff tick on a forgotten feed reports the same loss. One registration
            // answers all of them, so the ones that arrive while it is in flight are dropped
            // rather than turned into a storm of declarations.
            if (Interlocked.CompareExchange(ref registering, 1, 0) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    try
                    {
                        await registerFeedAgain!(cancellationToken).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // The subscriber was stopped while the feed was being declared.
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error declaring the feed to the replicator again.");
                    }
                }
                finally
                {
                    // Clear the gate before reconnecting, so a stream that reports the feed
                    // lost again is answered by a new declaration rather than dropped.
                    Interlocked.Exchange(ref registering, 0);
                }

                // Reconnect through the same backoff as any other failure. The feed's hash is
                // a function of its definition, so the declaration restored the same hash and
                // the stream resumes from the bookmark it stopped at.
                ScheduleReconnect(taskCompletionSource, cancellationToken);
            });
        }

        private void ScheduleReconnect(TaskCompletionSource<bool> taskCompletionSource, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            int attempt = Interlocked.Increment(ref reconnectAttempt);
            var delay = TimeSpanFromBackoff(attempt);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        ConnectToFeed(taskCompletionSource, cancellationToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // The subscriber was stopped while waiting to reconnect.
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error reconnecting to feed");
                }
            }, cancellationToken);
        }

        private TimeSpan TimeSpanFromBackoff(int attempt)
        {
            double factor = Math.Pow(2, Math.Max(0, attempt - 1));
            double milliseconds = Math.Min(reconnectInitialDelay.TotalMilliseconds * factor, reconnectMaxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}