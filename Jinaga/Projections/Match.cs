using System.Linq;
using System;
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
        public bool CanRunOnGraph => Conditions.All(c => c.CanRunOnGraph);

        public string ToDescriptiveString(int depth)
        {
            var indent = new string(' ', depth * 4);
            var conditions = String.Join("", Conditions.Select(c => c.ToDescriptiveString(Unknown.Name, depth + 1)));
            return $"{indent}{Unknown.Name}: {Unknown.Type} [\n{conditions}{indent}]\n";
        }

        public Match Apply(ImmutableDictionary<string, string> replacements)
        {
            return new Match(Unknown, Conditions.Select(c => c.Apply(replacements)).ToImmutableList());
        }
    }
}