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
    /// Pinning is what keeps the cost of absorbing a save proportional to the candidates
    /// the save produces rather than to the rows the watch holds. A read of the whole
    /// specification answers the same question about one candidate and makes every save
    /// cost as much as the full result.
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
                matches = matches.Add(PinMatch(match, index, pinName));
            }
            return (new Specification(givens, matches, specification.Projection), pinnedGivens);
        }

        private static string PinName(int index) => $"__p{index}";

        /// <summary>
        /// The match with its unknown pinned to one fact. The pin leads, so the store binds
        /// the unknown without enumerating anything, and the conditions that follow filter
        /// that one binding.
        ///
        /// A path condition with roles on its left walks forward from a fact the row
        /// already names, so checking it against a pinned unknown would enumerate that
        /// fact's successors -- for a one-level specification, the whole result set. Both
        /// of its sides are known facts here, so it becomes the question whether they reach
        /// a common predecessor, which each side answers by walking its own predecessors. A
        /// condition with no roles on its left already walks predecessors alone and carries
        /// over as it is.
        /// </summary>
        private static Match PinMatch(Match match, int index, string pinName)
        {
            var pathConditions = ImmutableList.Create(
                new PathCondition(ImmutableList<Role>.Empty, pinName, ImmutableList<Role>.Empty));
            var existentialConditions = ImmutableList<ExistentialCondition>.Empty;
            int join = 0;
            foreach (var pathCondition in match.PathConditions)
            {
                if (pathCondition.RolesLeft.IsEmpty)
                {
                    pathConditions = pathConditions.Add(pathCondition);
                }
                else
                {
                    existentialConditions = existentialConditions.Add(CommonPredecessor(
                        match.Unknown, pathCondition, $"__j{index}_{join}"));
                    join++;
                }
            }
            return new Match(
                match.Unknown,
                pathConditions,
                existentialConditions.AddRange(match.ExistentialConditions));
        }

        /// <summary>
        /// That <c>u-&gt;L = r-&gt;R</c> holds of two known facts, written as the fact both
        /// sides reach. The condition's own label is new to the specification, so it
        /// shadows nothing.
        /// </summary>
        private static ExistentialCondition CommonPredecessor(Label unknown, PathCondition pathCondition, string joinName)
        {
            var joinType = pathCondition.RolesLeft[pathCondition.RolesLeft.Count - 1].TargetType;
            return new ExistentialCondition(true, ImmutableList.Create(new Match(
                new Label(joinName, joinType),
                ImmutableList.Create(
                    new PathCondition(ImmutableList<Role>.Empty, unknown.Name, pathCondition.RolesLeft),
                    new PathCondition(ImmutableList<Role>.Empty, pathCondition.LabelRight, pathCondition.RolesRight)),
                ImmutableList<ExistentialCondition>.Empty)));
        }
    }
}
