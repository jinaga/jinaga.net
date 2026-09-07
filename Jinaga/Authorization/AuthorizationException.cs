using System;

namespace Jinaga.Authorization
{
    /// <summary>
    /// Thrown when a fact is refused by the authorization rules.
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
