using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jinaga.Http
{
    public class FeedsResponse
    {
        /// <summary>
        /// The feeds the replicator will serve. Required: a response that carries no such
        /// property, or carries one that is not an array, is malformed rather than a
        /// declaration of no feeds, so it is refused instead of read as an empty list or
        /// as a null this code would dereference later. Only values this client does not
        /// know are tolerated; a missing or mistyped member is not one.
        /// </summary>
        [JsonRequired]
        [JsonConverter(typeof(ProtocolListConverter<string>))]
        public List<string> Feeds { get; set; } = new List<string>();

        /// <summary>
        /// What the replicator decided about each feed. A replicator that does not
        /// report decisions sends no such property, which leaves this empty. A property
        /// that is present but not an array is malformed, as it is for <see cref="Feeds"/>.
        /// </summary>
        [JsonConverter(typeof(ProtocolListConverter<FeedDecision>))]
        public List<FeedDecision> Decisions { get; set; } = new List<FeedDecision>();
    }
}
