using Jinaga.Facts;
using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Pipelines
{
    /// <summary>
    /// The read that decides one candidate row.
    ///
    /// An inverse of <see cref="InverseCandidates"/> reports a row whose membership may
    /// have changed, never one whose membership did change. The observer decides each
    /// candidate by reading the specification again for that row alone: the watch's own
    /// givens, every unknown pinned to the fact the candidate bound to it, and every
    /// condition still applied. The read answers with that one row or with nothing, and
    /// the observer takes the answer as the row's membership.
    ///
    /// Pinning is what keeps a save from costing what the whole result costs. A read of
    /// the specification itself would answer the same question about one candidate and
    /// return every row the watch holds to do it.
    /// </summary>
    internal static class CandidateRecheck
    {
        /// <summary>
        /// The specification read to decide one candidate, and the givens to read it with.
        /// </summary>
        /// <param name="specification">The specification whose rows are being decided</param>
        /// <param name="givenTuple">The watch's own givens</param>
        /// <param name="candidate">A reference for each of the specification's unknowns</param>
        public static (Specification Specification, FactReferenceTuple Givens) Pin(
            Specification specification,
            FactReferenceTuple givenTuple,
            FactReferenceTuple candidate)
        {
            var givens = specification.Givens;
            var pinnedGivens = givenTuple;
            var matches = ImmutableList<Match>.Empty;
            for (int index = 0; index < specification.Matches.Count; index++)
            {
                var match = specification.Matches[index];
                var pinName = PinName(index);
                givens = givens.Add(new SpecificationGiven(
                    new Label(pinName, match.Unknown.Type),
                    ImmutableList<ExistentialCondition>.Empty));
                pinnedGivens = pinnedGivens.Add(pinName, candidate.Get(match.Unknown.Name));
                matches = matches.Add(PinMatch(match, pinName));
            }
            return (new Specification(givens, matches, specification.Projection), pinnedGivens);
        }

        private static string PinName(int index) => $"__p{index}";

        /// <summary>
        /// The match with its unknown pinned to one fact. The pin leads, so a store that
        /// walks the match binds the unknown without enumerating anything, and the
        /// conditions that follow filter that one binding.
        /// </summary>
        /// <remarks>
        /// Every condition carries over as it is. The pin is an equality rather than a step
        /// between facts, so it contributes no edge of its own: a match left with the pin
        /// alone would give the SQLite store's generator no clause to anchor its query on,
        /// and the conditions it keeps are what the generator joins.
        /// </remarks>
        private static Match PinMatch(Match match, string pinName)
        {
            var pathConditions = ImmutableList
                .Create(new PathCondition(ImmutableList<Role>.Empty, pinName, ImmutableList<Role>.Empty))
                .AddRange(match.PathConditions);
            return new Match(match.Unknown, pathConditions, match.ExistentialConditions);
        }
    }
}
