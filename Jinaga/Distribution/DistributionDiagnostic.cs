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
        /// Whether the feed is authorized, reactive, or denied.
        /// </summary>
        public DistributionDecision Decision => decision.Decision;

        /// <summary>
        /// Why the feed was denied, or null if it was not.
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
