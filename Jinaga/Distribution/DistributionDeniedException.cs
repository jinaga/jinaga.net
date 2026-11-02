using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga
{
    /// <summary>
    /// Thrown when a replicator denies a feed of a query for a structural reason:
    /// the specification and the distribution rules cannot agree, so no fact anyone
    /// later writes will change the answer. A denial that a later fact can change
    /// returns an empty result instead.
    /// </summary>
    public class DistributionDeniedException : Exception
    {
        public DistributionDeniedException(ImmutableList<DistributionDiagnostic> diagnostics)
            : base(DescribeDiagnostics(diagnostics))
        {
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// The structural denials that caused the throw.
        /// </summary>
        public ImmutableList<DistributionDiagnostic> Diagnostics { get; }

        private static string DescribeDiagnostics(ImmutableList<DistributionDiagnostic> diagnostics)
        {
            var denials = string.Join("\n", diagnostics
                .Select(diagnostic => $"  {diagnostic.Feed} ({diagnostic.Code}): {diagnostic.Reason}"));
            return $"The replicator denied the distribution of this specification:\n{denials}";
        }
    }
}
