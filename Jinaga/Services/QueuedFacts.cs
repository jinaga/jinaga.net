using Jinaga.Facts;
using System.Collections.Immutable;

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

        public QueuedFacts(FactGraph graph, ImmutableList<QueuedFact> facts)
        {
            Graph = graph;
            Facts = facts;
        }
    }
}
