using System.Collections.Generic;

namespace Jinaga.Http
{
    public class FeedsResponse
    {
        public List<string> Feeds { get; set; } = new List<string>();

        /// <summary>
        /// What the replicator decided about each feed. A replicator that does not
        /// report decisions sends no such property, which leaves this empty.
        /// </summary>
        public List<FeedDecision> Decisions { get; set; } = new List<FeedDecision>();
    }
}
