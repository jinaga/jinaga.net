namespace Jinaga
{
    /// <summary>
    /// The call that asked the replicator for a feed.
    /// </summary>
    public enum DistributionOperation
    {
        /// <summary>
        /// <see cref="JinagaClient"/>.Query or QueryWithDiagnostics.
        /// </summary>
        Query,

        /// <summary>
        /// <see cref="JinagaClient"/>.Watch.
        /// </summary>
        Watch,

        /// <summary>
        /// <see cref="JinagaClient"/>.Subscribe.
        /// </summary>
        Subscribe
    }
}
