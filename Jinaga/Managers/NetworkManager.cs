using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class NetworkManager
    {
        private readonly INetwork network;
        private readonly IStore store;
        private readonly Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers;

        private ImmutableDictionary<string, Task<ImmutableList<string>>> feedsCache =
            ImmutableDictionary<string, Task<ImmutableList<string>>>.Empty;
        private ImmutableDictionary<string, Task> activeFeeds =
            ImmutableDictionary<string, Task>.Empty;
        private int fetchCount = 0;
        private LoadBatch? currentBatch = null;

        public NetworkManager(INetwork network, IStore store, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers)
        {
            this.network = network;
            this.store = store;
            this.notifyObservers = notifyObservers;
        }

        public async Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            // TODO: Queue the facts for sending.
            // Send the facts using the network provider.
            await network.Save(facts, cancellationToken).ConfigureAwait(false);
        }

        public async Task Fetch(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var reducedSpecification = specification.Reduce();
            var feeds = await GetFeedsFromCache(givenTuple, reducedSpecification, cancellationToken).ConfigureAwait(false);

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
            }
            catch (Exception)
            {
                // If any feed fails, then remove the specification from the cache.
                RemoveFeedsFromCache(givenTuple, reducedSpecification);
                throw;
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
    }
}