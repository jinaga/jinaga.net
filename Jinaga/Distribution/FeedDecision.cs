using Jinaga.Http;
using System.Text.Json.Serialization;

namespace Jinaga
{
    /// <summary>
    /// What a replicator decided about one feed of a specification.
    /// </summary>
    public enum DistributionDecision
    {
        /// <summary>
        /// A distribution rule permits the feed, so its facts are sent.
        /// </summary>
        Authorized,

        /// <summary>
        /// The feed is permitted only while a condition holds, so a later fact can change the answer.
        /// </summary>
        Reactive,

        /// <summary>
        /// The feed is refused. <see cref="FeedDecision.Code"/> says why.
        /// </summary>
        Denied
    }

    /// <summary>
    /// Why a replicator denied a feed.
    /// </summary>
    public enum DistributionDenialCode
    {
        /// <summary>
        /// No distribution rule matches the specification.
        /// </summary>
        NoMatchingRule,

        /// <summary>
        /// A distribution rule matches, but the specification is narrower than the rule allows.
        /// </summary>
        SpecMoreRestrictiveThanRule,

        /// <summary>
        /// A distribution rule matches, but it excludes the principal who asked.
        /// </summary>
        PrincipalExcluded,

        /// <summary>
        /// A distribution rule matches, but nobody is authenticated.
        /// </summary>
        NotAuthenticated
    }

    /// <summary>
    /// One feed of a specification, and what the replicator decided about it.
    /// </summary>
    public class FeedDecision
    {
        /// <summary>
        /// The feed the decision is about.
        /// </summary>
        [JsonConverter(typeof(ProtocolStringConverter))]
        public string Feed { get; set; } = string.Empty;

        /// <summary>
        /// What the replicator decided, as it spelled it. Replicas are peers, and a
        /// replicator newer than this client can decide something this client has no
        /// name for, so the name is kept exactly as received rather than reduced to a
        /// member this client happens to know.
        /// </summary>
        [JsonPropertyName("decision")]
        [JsonConverter(typeof(ProtocolStringConverter))]
        public string DecisionName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the feed is authorized, reactive, or denied: <see cref="DistributionDecision.Denied"/>
        /// when the replicator sent a decision this client does not know, which is the safe
        /// reading, because no path treats a denied feed as authorized. <see cref="DecisionName"/>
        /// carries the name either way, and this is derived from it through the serializer's one
        /// table of protocol names rather than stored beside it.
        /// </summary>
        [JsonIgnore]
        public DistributionDecision Decision
        {
            get => MessageSerializer.DecisionOfName(DecisionName) ?? DistributionDecision.Denied;
            set => DecisionName = MessageSerializer.NameOfDecision(value);
        }

        /// <summary>
        /// Why the feed was denied, as the replicator spelled it, or null if it was not.
        /// Replicas are peers, and a replicator newer than this client can deny a feed for
        /// a reason this client has no name for, so the name is kept exactly as received
        /// rather than reduced to a member this client happens to know.
        /// </summary>
        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(ProtocolStringConverter))]
        public string? CodeName { get; set; }

        /// <summary>
        /// Why the feed was denied: null if it was not, and null if the replicator sent a
        /// code this client does not know. <see cref="CodeName"/> carries the name either
        /// way, and this is derived from it through the serializer's one table of protocol
        /// names rather than stored beside it.
        /// </summary>
        [JsonIgnore]
        public DistributionDenialCode? Code
        {
            get => MessageSerializer.DenialCodeOfName(CodeName);
            set => CodeName = value.HasValue ? MessageSerializer.NameOfDenialCode(value.Value) : null;
        }

        /// <summary>
        /// The replicator's own account of the decision.
        /// </summary>
        [JsonConverter(typeof(ProtocolStringConverter))]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Whether the denial is structural: the specification and the rules cannot
        /// agree, so no fact anyone later writes will change the answer. It holds only
        /// when <see cref="DecisionName"/> names a decision this client knows and
        /// <see cref="Code"/> is one of the two structural codes. Under a decision this
        /// client knows, the code decides, so a structural code makes the feed structural
        /// even when the replicator reports the decision as
        /// <see cref="DistributionDecision.Reactive"/>. Under a decision this client does
        /// not know, nothing is structural, whatever the code, because structurality is a
        /// claim about a decision whose meaning this client can read. A code this client
        /// does not know needs no rule of its own, because <see cref="Code"/> is null for
        /// one.
        /// This is the
        /// only statement of that predicate. Both
        /// <see cref="DistributionDiagnostic.IsStructural"/> and the throw in
        /// <see cref="JinagaClient.Query{TFact, TProjection}(Specification{TFact, TProjection}, TFact, System.Threading.CancellationToken)"/>
        /// read it rather than restating it.
        /// </summary>
        [JsonIgnore]
        public bool IsStructural =>
            MessageSerializer.DecisionOfName(DecisionName).HasValue &&
            (Code == DistributionDenialCode.NoMatchingRule ||
                Code == DistributionDenialCode.SpecMoreRestrictiveThanRule);
    }
}
