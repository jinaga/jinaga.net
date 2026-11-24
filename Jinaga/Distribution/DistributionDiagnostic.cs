namespace Jinaga
{
    /// <summary>
    /// One decision a replicator made about one feed, reported to the caller that asked for it.
    /// </summary>
    public class DistributionDiagnostic
    {
        private readonly FeedDecision decision;

        public DistributionDiagnostic(DistributionOperation operation, string specification, FeedDecision decision)
        {
            Operation = operation;
            Specification = specification;
            this.decision = decision;
        }

        /// <summary>
        /// The call that asked for the feed.
        /// </summary>
        public DistributionOperation Operation { get; }

        /// <summary>
        /// The specification that was sent, as the replicator received it.
        /// </summary>
        public string Specification { get; }

        /// <summary>
        /// The feed the decision is about.
        /// </summary>
        public string Feed => decision.Feed;

        /// <summary>
        /// What the replicator decided, as it spelled it. A decision this client does not
        /// know reaches the caller here, and <see cref="Decision"/> reads
        /// <see cref="DistributionDecision.Denied"/> for it.
        /// </summary>
        public string DecisionName => decision.DecisionName;

        /// <summary>
        /// Whether the feed is authorized, reactive, or denied, as
        /// <see cref="FeedDecision.Decision"/> gives it.
        /// </summary>
        public DistributionDecision Decision => decision.Decision;

        /// <summary>
        /// Why the feed was denied, as the replicator spelled it, or null if it was not.
        /// A code this client does not know reaches the caller here, and <see cref="Code"/>
        /// is null for it.
        /// </summary>
        public string? CodeName => decision.CodeName;

        /// <summary>
        /// Why the feed was denied, as <see cref="FeedDecision.Code"/> gives it: null if it
        /// was not, and null if the replicator sent a code this client does not know.
        /// </summary>
        public DistributionDenialCode? Code => decision.Code;

        /// <summary>
        /// The replicator's own account of the decision.
        /// </summary>
        public string Reason => decision.Reason;

        /// <summary>
        /// Whether the denial is structural, as <see cref="FeedDecision.IsStructural"/> defines it.
        /// </summary>
        public bool IsStructural => decision.IsStructural;
    }

    /// <summary>
    /// Receives a decision a replicator made about a feed.
    /// </summary>
    /// <param name="diagnostic">The decision</param>
    public delegate void DistributionDiagnosticHandler(DistributionDiagnostic diagnostic);
}
