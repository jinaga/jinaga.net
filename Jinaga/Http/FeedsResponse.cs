using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jinaga.Http
{
    public class FeedsResponse
    {
        /// <summary>
        /// The feeds the replicator will serve. Required: a response that carries no such
        /// property is malformed rather than a declaration of no feeds, so it is refused
        /// instead of read as an empty list. Only values this client does not know are
        /// tolerated; a missing member is not one.
        /// </summary>
        [JsonRequired]
        public List<string> Feeds { get; set; } = new List<string>();

        /// <summary>
        /// What the replicator decided about each feed. A replicator that does not
        /// report decisions sends no such property, which leaves this empty.
        /// </summary>
        public List<FeedDecision> Decisions { get; set; } = new List<FeedDecision>();
    }
}
