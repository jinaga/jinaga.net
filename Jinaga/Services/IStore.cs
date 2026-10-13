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
        /// Saves the facts in the graph, recording who authored the ones it queues.
        ///
        /// Each fact that is new to the store and queued for upload is recorded as
        /// authored by <paramref name="author"/>. A fact the store already holds records
        /// nothing, so a fact has at most one local author. The record is replica-local:
        /// it never enters a <see cref="FactEnvelope"/> or a <see cref="FactGraph"/>, so
        /// no serializer can carry it to a replicator.
        ///
        /// The record outlives the fact's queue entry. Acknowledging a fact removes it
        /// from the queue and leaves the authorship record in place.
        /// </summary>
        /// <param name="graph">The facts to save</param>
        /// <param name="queue">True to queue the new facts for upload</param>
        /// <param name="author">Public key of the user who is saving, or null if nobody is</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The facts that were added to the store</returns>
        Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, string? author, CancellationToken cancellationToken)
            => Save(graph, queue, cancellationToken);

        /// <summary>
        /// The queued facts that may be uploaded while <paramref name="currentAuthor"/> is
        /// the authenticated user.
        ///
        /// A queued fact is included only when its own author is
        /// <paramref name="currentAuthor"/> or it has none, and no queued ancestor of it
        /// has a different author. Every other queued fact is held: <see cref="GetQueue"/>
        /// still returns it, and it is included again once its author is current. An
        /// ancestor with no author travels with its descendant, because nobody claimed it.
        ///
        /// The bookmarks on the result still acknowledge a prefix of the queue, so one
        /// never runs past a held fact.
        /// </summary>
        /// <param name="currentAuthor">Public key of the authenticated user, or null if nobody is</param>
        /// <returns>The sendable queued facts, oldest first, with their ancestors</returns>
        Task<QueuedFacts> GetQueue(string? currentAuthor)
            => GetQueue();

        /// <summary>
        /// Removes the given facts from the outbound queue.
        ///
        /// A sender that holds some facts back acknowledges the ones it sent rather than a
        /// position in the queue, because the facts it sent are no longer a prefix of it.
        /// A store that does not route by author has only a prefix to advance, which is
        /// what this does by default.
        /// </summary>
        /// <param name="facts">The facts that were sent, oldest first</param>
        Task AcknowledgeQueue(ImmutableList<QueuedFact> facts)
            => facts.IsEmpty
                ? Task.CompletedTask
                : SetQueueBookmark(facts[facts.Count - 1].Bookmark);

        /// <summary>
        /// The public key of the user who authored each of the given facts on this replica,
        /// as recorded by <see cref="Save(FactGraph, bool, string?, CancellationToken)"/>.
        ///
        /// A replica signs nothing, so a fact this device created carries no signature until
        /// a replicator returns one, and the feed paths skip a fact the replica already holds.
        /// The authorship record is what this replica knows about who asserted such a fact,
        /// which is why provenance reads it alongside the stored signatures.
        ///
        /// Like a date learned, an author travels beside the graph rather than inside it, so
        /// nothing can serialize it onto the network. A fact with no recorded author is absent
        /// from the result, and so is a reference the store does not hold.
        ///
        /// A store that records no authors returns nothing, which leaves provenance reading
        /// the stored signatures alone.
        /// </summary>
        /// <param name="references">The facts whose local authors to read</param>
        /// <param name="cancellationToken">To cancel the operation</param>
        /// <returns>The public key of each fact's local author, where it has one</returns>
        Task<ImmutableDictionary<FactReference, string>> LoadLocalAuthors(ImmutableList<FactReference> references, CancellationToken cancellationToken)
            => Task.FromResult(ImmutableDictionary<FactReference, string>.Empty);
    }
}
