using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Projections
{
    public class Match
    {
        public Match(Label unknown, ImmutableList<MatchCondition> conditions)
        {
            Unknown = unknown;
            Conditions = conditions;
        }

        public Label Unknown { get; }
        public ImmutableList<MatchCondition> Conditions { get; }
    }
}