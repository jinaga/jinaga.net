using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface IStore
    {
        bool IsPersistent { get; }

        Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken);
        Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken);
        Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken);

        /// <summary>
        /// The UTC moment at which this store first saved each of the given facts.
        ///
        /// The dates are replica-local and are deliberately kept out of <see cref="FactGraph"/>,
        /// so that nothing can serialize them onto the network. A reference the store does not
        /// hold is absent from the result.
        /// </summary>
        Task<ImmutableDictionary<FactReference, DateTime>> LoadDatesLearned(ImmutableList<FactReference> references, CancellationToken cancellationToken);
        Task<string> LoadBookmark(string feed);
        Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences);
        Task SaveBookmark(string feed, string bookmark);
        Task<DateTime?> GetMruDate(string specificationHash);
        Task SetMruDate(string specificationHash, DateTime mruDate);
        Task<QueuedFacts> GetQueue();
        Task SetQueueBookmark(string bookmark);
        Task<IEnumerable<Fact>> GetAllFacts();
        Task Purge(ImmutableList<Specification> purgeConditions);
        Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers);

        /// <summary>
        /// Saves the facts in the graph, recording that <paramref name="savedBy"/> saved them.
        ///
        /// When <paramref name="queue"/> is true and <paramref name="savedBy"/> is not null, the
        /// store records the pair (fact, <paramref name="savedBy"/>) for the graph's last fact,
        /// which is the one the user asked to save, and for every fact this save adds to the
        /// store. It records the pair even when the store already holds the last fact: a second
        /// user saving a fact that a first user saved is something this replica witnessed, and
        /// it is what lets the second user's descendants of that fact travel without the first.
        /// A predecessor the store already holds gains no saver, because the user referred to
        /// it rather than saved it.
        ///
        /// The relation is replica-local: it never enters a <see cref="FactEnvelope"/> or a
        /// <see cref="FactGraph"/>, so no serializer can carry it to a replicator. It outlives
        /// the fact's queue entry, and goes only when the fact is purged.
        /// </summary>
        /// <param name="graph">The facts to save</param>
        /// <param name="queue">True to queue the new facts for upload</param>
        /// <param name="savedBy">Public key of the user who is saving, or null if nobody is</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The facts that were added to the store</returns>
        Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, string? savedBy, CancellationToken cancellationToken)
            => Save(graph, queue, cancellationToken);

        /// <summary>
        /// The public keys of the users this replica saw save each of the given facts.
        ///
        /// A fact nobody saved while logged in, and a reference the store does not hold, are
        /// absent from the result. A store that records no savers returns an empty result,
        /// which makes every queued fact sendable under whoever is logged in.
        ///
        /// A store that records savers must also implement <see cref="AcknowledgeQueue"/>,
        /// because the queue may then send facts that are not a prefix of it.
        /// </summary>
        Task<ImmutableDictionary<FactReference, ImmutableHashSet<string>>> LoadSavers(ImmutableList<FactReference> references, CancellationToken cancellationToken)
            => Task.FromResult(ImmutableDictionary<FactReference, ImmutableHashSet<string>>.Empty);

        /// <summary>
        /// Removes the given facts from the outbound queue.
        ///
        /// A sender that holds some facts back acknowledges the ones it sent rather than a
        /// position in the queue, because the facts it sent are no longer a prefix of it.
        /// A store that records no savers never has a fact held back, so it has only a prefix
        /// to advance, which is what this does by default.
        /// </summary>
        /// <param name="facts">The facts that were sent, oldest first</param>
        Task AcknowledgeQueue(ImmutableList<QueuedFact> facts)
            => facts.IsEmpty
                ? Task.CompletedTask
                : SetQueueBookmark(facts[facts.Count - 1].Bookmark);
    }
}
