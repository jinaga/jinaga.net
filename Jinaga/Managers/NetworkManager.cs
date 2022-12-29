using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class NetworkManager
    {
        private readonly INetwork network;
        private readonly IStore store;
        private readonly Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers;

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

        public async Task Query(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
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
    }
}