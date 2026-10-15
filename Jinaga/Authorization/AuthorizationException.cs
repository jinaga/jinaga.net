using System;

namespace Jinaga.Authorization
{
    /// <summary>
    /// Thrown when a fact is refused by the authorization rules.
    ///
    /// A client configured with <see cref="JinagaClientOptions.AuthorizationRules"/> throws this
    /// from <c>j.Fact</c> when the logged-in user may not author the fact, before anything is
    /// stored or queued. A rule that walks only predecessors is decided exactly, because a
    /// fact's predecessors are saved with it. A rule that walks successors, such as "a member of
    /// the project", reads the local store, so a replica that has not loaded the facts that
    /// authorize the user refuses a save the replicator would accept. Query or load those facts
    /// before saving.
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
