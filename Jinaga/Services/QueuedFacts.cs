using Jinaga.Facts;
using System.Collections.Immutable;

namespace Jinaga.Services
{
    public class QueuedFacts
    {
        public ImmutableList<Fact> Facts { get; }
        public string NextBookmark { get; }

        public QueuedFacts(ImmutableList<Fact> facts, string nextBookmark)
        {
            Facts = facts;
            NextBookmark = nextBookmark;
        }
    }
}