#nullable enable

using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// An IStore that overrides none of the members the saved-by relation added, which is
    /// what every implementation outside this repository looks like until its author takes
    /// them up. The default implementations are what has to keep it working: it records no
    /// savers, so the queue has nothing to route by and drains under whoever is logged in.
    /// </summary>
    internal class StoreWithoutSaverRouting : IStore
    {
        private readonly IStore inner;

        public StoreWithoutSaverRouting(IStore inner)
        {
            this.inner = inner;
        }

        public bool IsPersistent => true;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken) =>
            inner.Save(graph, queue, cancellationToken);

        public Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken) =>
            inner.Read(givenTuple, specification, cancellationToken);

        public Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken) =>
            inner.Load(references, cancellationToken);

        public Task<ImmutableDictionary<FactReference, DateTime>> LoadDatesLearned(ImmutableList<FactReference> references, CancellationToken cancellationToken) =>
            inner.LoadDatesLearned(references, cancellationToken);

        public Task<string> LoadBookmark(string feed) => inner.LoadBookmark(feed);

        public Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences) =>
            inner.ListKnown(factReferences);

        public Task SaveBookmark(string feed, string bookmark) => inner.SaveBookmark(feed, bookmark);

        public Task<DateTime?> GetMruDate(string specificationHash) => inner.GetMruDate(specificationHash);

        public Task SetMruDate(string specificationHash, DateTime mruDate) => inner.SetMruDate(specificationHash, mruDate);

        public Task<QueuedFacts> GetQueue() => inner.GetQueue();

        public Task SetQueueBookmark(string bookmark) => inner.SetQueueBookmark(bookmark);

        public Task<IEnumerable<Fact>> GetAllFacts() => inner.GetAllFacts();

        public Task Purge(ImmutableList<Specification> purgeConditions) => inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
    }
}
