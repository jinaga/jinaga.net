using System;

namespace Jinaga.Authorization
{
    /// <summary>
    /// Thrown when a fact is refused by the authorization rules.
    ///
    /// A client created by <c>Jinaga.UnitTest</c> with <c>JinagaTestOptions.Authorization</c>
    /// throws this from <c>j.Fact</c> when the configured user may not author the fact, before
    /// anything is stored or queued. No other client throws it: a production client evaluates no
    /// rules and leaves authorization to the replicator.
    ///
    /// A rule that walks only predecessors is decided exactly, because a fact's predecessors are
    /// saved with it. A rule that walks successors, such as "a member of the project", reads the
    /// local store, so a test whose store has not been seeded with the facts that authorize the
    /// user refuses a save the replicator would accept. Seed them with
    /// <c>JinagaTestOptions.InitialState</c>.
    ///
    /// The message names the fact type and nothing else, matching what a replicator reports.
    /// Saying which rule refused it, or which users would have been permitted, would turn a
    /// refusal into a way to probe the policy.
    /// </summary>
    public class AuthorizationException : Exception
    {
        public string FactType { get; }

        public AuthorizationException(string factType)
            : base($"The user is not authorized to create a fact of type {factType}.")
        {
            FactType = factType;
        }
    }
}
