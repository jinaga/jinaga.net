using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Services
{
    /// <summary>
    /// One fact waiting in the outbound queue.
    /// </summary>
    public class QueuedFact
    {
        /// <summary>
        /// The queued fact.
        /// </summary>
        public FactReference Reference { get; }

        /// <summary>
        /// The bookmark that acknowledges this fact and every fact queued
        /// before it. A sender that stops part way through the queue records
        /// the bookmark of the last fact it sent, so the next attempt resumes
        /// from there instead of starting over.
        /// </summary>
        public string Bookmark { get; }

        public QueuedFact(FactReference reference, string bookmark)
        {
            Reference = reference;
            Bookmark = bookmark;
        }
    }

    public class QueuedFacts
    {
        /// <summary>
        /// The queued facts together with their ancestors.
        /// </summary>
        public FactGraph Graph { get; }

        /// <summary>
        /// The queued facts, oldest first. Ancestors that were queued and sent
        /// earlier are in the graph but not in this list.
        /// </summary>
        public ImmutableList<QueuedFact> Facts { get; }

        /// <summary>
        /// True for a queue built with the obsolete constructor. Its one
        /// bookmark acknowledges the whole graph, so it must be sent as a
        /// single request: acknowledging after a partial send would drop the
        /// facts not yet sent.
        /// </summary>
        internal bool IsIndivisible { get; }

        private readonly string? legacyBookmark;

        public QueuedFacts(FactGraph graph, ImmutableList<QueuedFact> facts)
        {
            Graph = graph;
            Facts = facts;
            IsIndivisible = false;
            legacyBookmark = null;
        }

        /// <summary>
        /// Builds a queue the way a store compiled against Jinaga 1.1.54 or
        /// earlier does: one graph, and the bookmark that acknowledges all of
        /// it. Kept so that such a store, such as Jinaga.Store.SQLite 1.1.19,
        /// still runs against this version.
        /// </summary>
        [Obsolete("Use QueuedFacts(FactGraph, ImmutableList<QueuedFact>), which gives each queued fact its own bookmark.")]
        public QueuedFacts(FactGraph graph, string nextBookmark)
        {
            Graph = graph;
            Facts = graph.FactReferences
                .Select(reference => new QueuedFact(reference, nextBookmark))
                .ToImmutableList();
            IsIndivisible = true;
            legacyBookmark = nextBookmark;
        }

        /// <summary>
        /// The bookmark that acknowledges every fact in this queue.
        /// </summary>
        [Obsolete("Acknowledge the facts that were sent with IStore.AcknowledgeQueue, using the bookmarks in Facts.")]
        public string NextBookmark => legacyBookmark ??
            (Facts.IsEmpty ? string.Empty : Facts[Facts.Count - 1].Bookmark);
    }
}
