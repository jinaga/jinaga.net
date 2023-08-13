using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Products;
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

        private ImmutableDictionary<string, ImmutableList<string>> feedsCache =
            ImmutableDictionary<string, ImmutableList<string>>.Empty;
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
            await network.Save(facts, cancellationToken);
        }

        public async Task Fetch(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
        {
            // Get the feeds from the source.
            var feeds = await network.Feeds(givenReferences, specification, cancellationToken);

            // TODO: Fork to fetch from each feed.
            foreach (var feed in feeds)
            {
                // Load the bookmark.
                string bookmark = await store.LoadBookmark(feed);

                while (true)
                {
                    // Fetch facts from the feed starting at the bookmark.
                    (var factReferences, var nextBookmark) = await network.FetchFeed(feed, bookmark, cancellationToken);
                    
                    // If there are no facts, end.
                    if (factReferences.Count == 0)
                    {
                        break;
                    }

                    // Load the facts that I don't already have.
                    var knownFactReferences = await store.ListKnown(factReferences);
                    var graph = await network.Load(factReferences.RemoveRange(knownFactReferences), cancellationToken);

                    // Save the facts.
                    var added = await store.Save(graph, cancellationToken);

                    // Notify observers.
                    await notifyObservers(graph, added, cancellationToken);

                    // Update the bookmark.
                    bookmark = nextBookmark;
                    await store.SaveBookmark(feed, bookmark);
                }
            }
        }

        public async Task Fetch(Product givenProduct, Specification specification, CancellationToken cancellationToken)
        {
            var reducedSpecification = specification.Reduce();
            var feeds = await GetFeedsFromCache(givenProduct, reducedSpecification, cancellationToken);

            // Fork to fetch from each feed.
            var tasks = feeds.Select(feed =>
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
            });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                // If any feed fails, then remove the specification from the cache.
                RemoveFeedsFromCache(givenProduct, reducedSpecification);
                throw;
            }
        }

        private async Task ProcessFeed(string feed, CancellationToken cancellationToken)
        {
            // Load the bookmark.
            string bookmark = await store.LoadBookmark(feed);

            while (true)
            {
                fetchCount++;
                bool decremented = false;

                try
                {
                    // Fetch facts from the feed starting at the bookmark.
                    (var factReferences, var nextBookmark) = await network.FetchFeed(feed, bookmark, cancellationToken);

                    // If there are no facts, end.
                    if (factReferences.Count == 0)
                    {
                        break;
                    }

                    // Load the facts that I don't already have.
                    var knownFactReferences = await store.ListKnown(factReferences);
                    var unknownFactReferences = factReferences.RemoveRange(knownFactReferences);
                    if (unknownFactReferences.Any())
                    {
                        var batch = currentBatch;
                        if (batch == null)
                        {
                            // Begin a new batch.
                            batch = new LoadBatch(network, store, notifyObservers, BatchStarted);
                            currentBatch = batch;
                        }
                        batch.Add(unknownFactReferences);
                        fetchCount--;
                        decremented = true;
                        if (fetchCount == 0)
                        {
                            // This is the last fetch, so trigger the batch.
                            batch.Trigger();
                        }
                        await batch.Completed;
                    }

                    // Update the bookmark.
                    bookmark = nextBookmark;
                    await store.SaveBookmark(feed, bookmark);
                }
                finally
                {
                    if (!decremented)
                    {
                        fetchCount--;
                        if (fetchCount == 0 && currentBatch != null)
                        {
                            // This is the last fetch, so trigger the batch.
                            currentBatch.Trigger();
                        }
                    }
                }
            }
        }

        private void BatchStarted(LoadBatch batch)
        {
            if (batch == currentBatch)
            {
                currentBatch = null;
            }
        }

        private async Task<ImmutableList<string>> GetFeedsFromCache(Product givenProduct, Specification specification, CancellationToken cancellationToken)
        {
            var hash = IdentityUtilities.ComputeSpecificationHash(specification, givenProduct);
            if (feedsCache.TryGetValue(hash, out var cached))
            {
                return cached;
            }
            var feeds = await network.Feeds(givenProduct, specification, cancellationToken);
            feedsCache = feedsCache.Add(hash, feeds);
            return feeds;
        }

        private void RemoveFeedsFromCache(Product givenProduct, Specification specification)
        {
            var hash = IdentityUtilities.ComputeSpecificationHash(specification, givenProduct);
            feedsCache = feedsCache.Remove(hash);
        }
    }
}