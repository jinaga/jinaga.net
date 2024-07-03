using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Projections;
using Jinaga.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class NetworkManager
    {
        private readonly INetwork network;
        private readonly IStore store;
        private readonly ILogger logger;
        private readonly Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers;

        private ImmutableDictionary<string, Task<ImmutableList<string>>> feedsCache =
            ImmutableDictionary<string, Task<ImmutableList<string>>>.Empty;
        private ImmutableDictionary<string, Task> activeFeeds =
            ImmutableDictionary<string, Task>.Empty;
        private ImmutableDictionary<string, Subscriber> subscribers =
            ImmutableDictionary<string, Subscriber>.Empty;
        private int fetchCount = 0;
        private LoadBatch? currentBatch = null;
        private JinagaStatus status = JinagaStatus.Default;

        public event JinagaStatusChanged? OnStatusChanged;

        public NetworkManager(INetwork network, IStore store, ILoggerFactory loggerFactory, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers)
        {
            this.network = network;
            this.store = store;
            this.logger = loggerFactory.CreateLogger<NetworkManager>();
            this.notifyObservers = notifyObservers;
        }

        public async Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            return await network.Login(cancellationToken).ConfigureAwait(false);
        }

        public async Task Save(CancellationToken cancellationToken)
        {
            // Get the queued facts.
            var queue = await store.GetQueue().ConfigureAwait(false);
            if (queue.Graph.FactReferences.Count == 0)
            {
                SetSaveStatus(false, null, 0);
                return;
            }
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            logger.LogInformation("Save started with {0} facts.", queue.Graph.FactReferences.Count);
            SetSaveStatus(true, null, queue.Graph.FactReferences.Count);

            try
            {
                // Send the facts using the network provider.
                await network.Save(queue.Graph, cancellationToken).ConfigureAwait(false);
                // Update the queue.
                await store.SetQueueBookmark(queue.NextBookmark).ConfigureAwait(false);
                logger.LogInformation("Save completed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, null, 0);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Save failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, ex, queue.Graph.FactReferences.Count);
                throw;
            }
        }

        public async Task Fetch(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SetLoadStatus(true, null);

            // Retry once. A feed might be cached and need to be removed and re-added.
            int retryCount = 1;

            while (true)
            {
                try
                {
                    await FetchInternal(givenTuple, specification, stopwatch, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    retryCount--;
                    if (retryCount > 0)
                    {
                        logger.LogWarning(ex, "Fetch failed after {elapsedMilliseconds} ms. Retrying.", stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogError(ex, "Fetch failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                        SetLoadStatus(false, ex);
                        throw;
                    }
                }
            }
        }

        private async Task FetchInternal(FactReferenceTuple givenTuple, Specification specification, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            var reducedSpecification = specification.Reduce();
            var feeds = await GetFeedsFromCache(givenTuple, reducedSpecification, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Fetch from {0} feeds.", feeds.Count);

            // Fork to fetch from each feed.
            var tasks = feeds.Select(feed =>
            {
                lock (this)
                {
                    if (activeFeeds.TryGetValue(feed, out var task))
                    {
                        return task;
                    }
                    else
                    {
                        task = Task.Run(() => ProcessFeed(feed, cancellationToken));
                        activeFeeds = activeFeeds.Add(feed, task);
                        return task;
                    }
                }
            });

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
                logger.LogInformation("Fetch completed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetLoadStatus(false, null);
            }
            catch
            {
                // If any feed fails, then remove the specification from the cache.
                RemoveFeedsFromCache(givenTuple, reducedSpecification);
                throw;
            }
        }

        public async Task<ImmutableList<string>> Subscribe(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Retry once. A feed might be cached and need to be removed and re-added.
            int retryCount = 1;

            while (true)
            {
                try
                {
                    return await SubscribeInternal(givenTuple, specification, stopwatch, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    retryCount--;
                    if (retryCount > 0)
                    {
                        logger.LogWarning(ex, "Subscribe failed after {elapsedMilliseconds} ms. Retrying.", stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogError(ex, "Subscribe failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                        throw;
                    }
                }
            }
        }

        private async Task<ImmutableList<string>> SubscribeInternal(FactReferenceTuple givenTuple, Specification specification, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            var reducedSpecification = specification.Reduce();
            var feeds = await GetFeedsFromCache(givenTuple, reducedSpecification, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Subscribe to {0} feeds.", feeds.Count);

            List<Subscriber> subscribers;
            lock (this)
            {
                subscribers = feeds.Select(feed =>
                {
                    if (!this.subscribers.TryGetValue(feed, out var subscriber))
                    {
                        subscriber = new Subscriber(feed, this.network, this.store, this.logger, this.notifyObservers);
                        this.subscribers = this.subscribers.Add(feed, subscriber);
                    }
                    return subscriber;
                }).ToList();
            }

            var tasks = subscribers.Select(async subscriber =>
            {
                if (subscriber.AddRef())
                {
                    await subscriber.Start();
                }
            });

            try
            {
                await Task.WhenAll(tasks);
                logger.LogInformation("Subscribe initialized after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Subscribe failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                // If any feed fails, then remove the specification from the cache.
                this.RemoveFeedsFromCache(givenTuple, reducedSpecification);
                this.Unsubscribe(feeds);
                throw e;
            }
            return feeds;
        }

        public void Unsubscribe(ImmutableList<string> feeds)
        {
            if (feeds.Count == 0)
            {
                return;
            }
            logger.LogInformation("Unsubscribe from {0} feeds.", feeds.Count);
            lock (this)
            {
                foreach (var feed in feeds)
                {
                    if (subscribers.TryGetValue(feed, out var subscriber))
                    {
                        if (subscriber.Release())
                        {
                            subscriber.Stop();
                            subscribers = subscribers.Remove(feed);
                        }
                    }
                }
            }
        }

        private async Task ProcessFeed(string feed, CancellationToken cancellationToken)
        {
            // Load the bookmark.
            string bookmark = await store.LoadBookmark(feed).ConfigureAwait(false);

            while (true)
            {
                Interlocked.Increment(ref fetchCount);
                bool decremented = false;

                try
                {
                    // Fetch facts from the feed starting at the bookmark.
                    (var factReferences, var nextBookmark) = await network.FetchFeed(feed, bookmark, cancellationToken).ConfigureAwait(false);

                    // If there are no facts, end.
                    if (factReferences.Count == 0)
                    {
                        lock (this)
                        {
                            activeFeeds = activeFeeds.Remove(feed);
                        }
                        break;
                    }

                    // Load the facts that I don't already have.
                    var knownFactReferences = await store.ListKnown(factReferences).ConfigureAwait(false);
                    var unknownFactReferences = factReferences.RemoveRange(knownFactReferences);
                    if (unknownFactReferences.Any())
                    {
                        var batch = GetCurrentBatch();
                        batch.Add(unknownFactReferences);
                        var finalFetchCount = Interlocked.Decrement(ref fetchCount);
                        decremented = true;
                        if (finalFetchCount == 0)
                        {
                            // This is the last fetch, so trigger the batch.
                            batch.Trigger();
                        }
                        await batch.Completed.ConfigureAwait(false);
                    }

                    // Update the bookmark.
                    bookmark = nextBookmark;
                    await store.SaveBookmark(feed, bookmark).ConfigureAwait(false);
                }
                finally
                {
                    if (!decremented)
                    {
                        var finalFetchCount = Interlocked.Decrement(ref fetchCount);
                        if (finalFetchCount == 0)
                        {
                            lock (this)
                            {
                                if (currentBatch != null)
                                {
                                    // This is the last fetch, so trigger the batch.
                                    currentBatch.Trigger();
                                }
                            }
                        }
                    }
                }
            }
        }

        private LoadBatch GetCurrentBatch()
        {
            lock (this)
            {
                var batch = currentBatch;
                if (batch == null)
                {
                    // Begin a new batch.
                    batch = new LoadBatch(network, store, notifyObservers, BatchStarted);
                    currentBatch = batch;
                }

                return batch;
            }
        }

        private void BatchStarted(LoadBatch batch)
        {
            lock (this)
            {
                if (batch == currentBatch)
                {
                    currentBatch = null;
                }
            }
        }

        private Task<ImmutableList<string>> GetFeedsFromCache(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var hash = IdentityUtilities.ComputeSpecificationHash(specification, givenTuple);
                if (feedsCache.TryGetValue(hash, out var cached))
                {
                    if (!cached.IsFaulted)
                    {
                        return cached;
                    }
                    feedsCache = feedsCache.Remove(hash);
                }
                var feeds = network.Feeds(givenTuple, specification, cancellationToken);
                feedsCache = feedsCache.Add(hash, feeds);
                return feeds;
            }
        }

        private void RemoveFeedsFromCache(FactReferenceTuple givenTuple, Specification specification)
        {
            var hash = IdentityUtilities.ComputeSpecificationHash(specification, givenTuple);
            lock (this)
            {
                feedsCache = feedsCache.Remove(hash);
            }
        }

        private void SetLoadStatus(bool isLoading, Exception? lastLoadError)
        {
            lock (this)
            {
                status = status.WithLoadStatus(isLoading, lastLoadError);
                OnStatusChanged?.Invoke(status);
            }
        }

        private void SetSaveStatus(bool isSaving, Exception? lastSaveError, int queueLength)
        {
            lock (this)
            {
                status = status.WithSaveStatus(isSaving, lastSaveError, queueLength);
                OnStatusChanged?.Invoke(status);
            }
        }
    }
}