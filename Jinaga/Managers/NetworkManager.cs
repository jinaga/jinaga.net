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
        private readonly int maxBatchSize;

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

        public NetworkManager(INetwork network, IStore store, ILoggerFactory loggerFactory, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers, int maxBatchSize)
        {
            if (maxBatchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize), maxBatchSize, "The maximum batch size must be greater than zero.");

            this.network = network;
            this.store = store;
            this.logger = loggerFactory.CreateLogger<NetworkManager>();
            this.notifyObservers = notifyObservers;
            this.maxBatchSize = maxBatchSize;

            network.OnAuthenticationStateChanged += SetAuthenticationState;
        }

        public async Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            return await network.Login(cancellationToken).ConfigureAwait(false);
        }

        public async Task Save(CancellationToken cancellationToken)
        {
            // Get the queued facts.
            var queue = await store.GetQueue().ConfigureAwait(false);
            if (queue.Facts.Count == 0)
            {
                SetSaveStatus(false, null, 0);
                return;
            }
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            logger.LogInformation("Save started with {0} facts.", queue.Facts.Count);
            SetSaveStatus(true, null, queue.Facts.Count);

            int remaining = queue.Facts.Count;
            try
            {
                // Send the facts in batches, so that a queue which has grown
                // past a request size limit between here and the replicator
                // still drains. A batch that succeeds advances the bookmark,
                // so a failure part way through leaves the batches that
                // succeeded out of the queue and the rest in it.
                foreach (var batch in SplitIntoBatches(queue))
                {
                    if (batch.Graph.FactReferences.Count > 0)
                    {
                        await network.Save(batch.Graph, cancellationToken).ConfigureAwait(false);
                    }
                    await store.SetQueueBookmark(batch.Bookmark).ConfigureAwait(false);
                    remaining -= batch.Count;
                    if (remaining > 0)
                    {
                        SetSaveStatus(true, null, remaining);
                    }
                }
                logger.LogInformation("Save completed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, null, 0);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Save failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, ex, remaining);
                throw;
            }
        }

        private class SaveBatch
        {
            public FactGraph Graph { get; }
            public string Bookmark { get; }
            public int Count { get; }

            public SaveBatch(FactGraph graph, string bookmark, int count)
            {
                Graph = graph;
                Bookmark = bookmark;
                Count = count;
            }
        }

        /// <summary>
        /// Divides the queue into consecutive batches of at most maxBatchSize
        /// facts, oldest first.
        ///
        /// A batch carries the ancestors of the facts it sends, including ones
        /// an earlier batch already sent, because the graph wire format names a
        /// predecessor by its position within the same request. The bound
        /// therefore applies to the whole request rather than to the facts that
        /// are new to it, which is what makes it a bound on request size.
        ///
        /// A single queued fact whose ancestors already exceed the bound is
        /// sent on its own and over the bound, so that the queue still drains.
        /// </summary>
        private IEnumerable<SaveBatch> SplitIntoBatches(QueuedFacts queue)
        {
            // A store can report a queued fact that its graph does not carry,
            // if an ancestor of that fact is missing. Such a fact could not be
            // sent before this change either, so the bookmark still moves past
            // it rather than stalling the queue on it.
            var available = new HashSet<FactReference>(queue.Graph.FactReferences);

            int index = 0;
            while (index < queue.Facts.Count)
            {
                var graph = FactGraph.Empty;
                var included = new HashSet<FactReference>();
                string bookmark = queue.Facts[index].Bookmark;
                int count = 0;
                while (index < queue.Facts.Count)
                {
                    var reference = queue.Facts[index].Reference;
                    if (available.Contains(reference))
                    {
                        var additions = AncestorsNotIncluded(queue.Graph, reference, included);
                        if (count > 0 && included.Count + additions.Count > maxBatchSize)
                        {
                            break;
                        }
                        foreach (var addition in additions)
                        {
                            graph = graph.Add(queue.Graph.GetEnvelope(addition));
                            included.Add(addition);
                        }
                    }
                    bookmark = queue.Facts[index].Bookmark;
                    count++;
                    index++;
                }
                if (included.Count > maxBatchSize)
                {
                    logger.LogWarning("A queued fact and its ancestors are {0} facts, which exceeds the maximum batch size of {1}. Sending it as one batch.", included.Count, maxBatchSize);
                }
                yield return new SaveBatch(graph, bookmark, count);
            }
        }

        /// <summary>
        /// The fact and the ancestors it needs that the batch does not already
        /// have, in topological order.
        /// </summary>
        private static ImmutableList<FactReference> AncestorsNotIncluded(FactGraph graph, FactReference reference, HashSet<FactReference> included)
        {
            var additions = ImmutableList.CreateBuilder<FactReference>();
            var visited = new HashSet<FactReference>();
            Visit(graph, reference, included, visited, additions);
            return additions.ToImmutable();
        }

        private static void Visit(FactGraph graph, FactReference reference, HashSet<FactReference> included, HashSet<FactReference> visited, ImmutableList<FactReference>.Builder additions)
        {
            if (included.Contains(reference) || !visited.Add(reference))
            {
                return;
            }
            foreach (var predecessor in graph.GetFact(reference).GetAllPredecessorReferences())
            {
                Visit(graph, predecessor, included, visited, additions);
            }
            additions.Add(reference);
        }

        public async Task Fetch(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SetLoadStatus(true, null);

            // Retry once. A feed might be cached and need to be removed and re-added.
            int retryCount = 2;

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
            int retryCount = 2;

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
                        subscriber = new Subscriber(feed, this.network, this.store, this.logger, this.notifyObservers,
                            cancellationToken => RegisterFeedsAgain(givenTuple, reducedSpecification, cancellationToken));
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
            try
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

                        // If there are no facts, save the (possibly advanced) bookmark
                        // and end, so a subsequent fetch does not redundantly re-request
                        // from a stale bookmark.
                        if (factReferences.Count == 0)
                        {
                            bookmark = nextBookmark;
                            await store.SaveBookmark(feed, bookmark).ConfigureAwait(false);
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
            finally
            {
                // Remove the feed from the active set on every exit path (success,
                // exception, or cancellation) so a transient failure does not
                // permanently disable the feed. A subsequent fetch will retry it.
                lock (this)
                {
                    activeFeeds = activeFeeds.Remove(feed);
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
                    // A cancelled declaration is not a faulted one, and a declaration made for a
                    // subscriber carries that subscriber's connection token, which its refresh
                    // timer cancels every few minutes. Serving either back would hand every later
                    // caller the same failure.
                    if (!cached.IsFaulted && !cached.IsCanceled)
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

        /// <summary>
        /// Declares the feeds of a specification to the replicator again, after it has
        /// forgotten one of them. The cached feed list is evicted first, so a concurrent
        /// Fetch or Subscribe of the same specification waits for this declaration rather
        /// than reusing a feed the replicator no longer knows.
        /// </summary>
        private async Task RegisterFeedsAgain(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            RemoveFeedsFromCache(givenTuple, specification);
            await GetFeedsFromCache(givenTuple, specification, cancellationToken).ConfigureAwait(false);
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

        private void SetAuthenticationState(JinagaAuthenticationState authenticationState)
        {
            lock (this)
            {
                status = status.WithAuthenticationState(authenticationState);
                OnStatusChanged?.Invoke(status);
            }
        }
    }
}