using Jinaga.Facts;

namespace Jinaga.Services
{
    public class QueuedFacts
    {
        public FactGraph Graph { get; }
        public string NextBookmark { get; }

        public QueuedFacts(FactGraph graph, string nextBookmark)
        {
            Graph = graph;
            NextBookmark = nextBookmark;
        }
    }
}