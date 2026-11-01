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
        public string Feed { get; set; } = string.Empty;

        /// <summary>
        /// Whether the feed is authorized, reactive, or denied.
        /// </summary>
        public DistributionDecision Decision { get; set; }

        /// <summary>
        /// Why the feed was denied, or null if it was not.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DistributionDenialCode? Code { get; set; }

        /// <summary>
        /// The replicator's own account of the decision.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Whether the denial is structural: the specification and the rules cannot
        /// agree, so no fact anyone later writes will change the answer. This is the
        /// only statement of that predicate. Both
        /// <see cref="DistributionDiagnostic.IsStructural"/> and the throw in
        /// <see cref="JinagaClient.Query{TFact, TProjection}(Specification{TFact, TProjection}, TFact, System.Threading.CancellationToken)"/>
        /// read it rather than restating it.
        /// </summary>
        [JsonIgnore]
        public bool IsStructural =>
            Code == DistributionDenialCode.NoMatchingRule ||
            Code == DistributionDenialCode.SpecMoreRestrictiveThanRule;
    }
}
