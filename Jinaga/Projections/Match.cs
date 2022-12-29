using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;
using System.Linq;

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
        public bool CanRunOnGraph => Conditions.Count == 1 && Conditions.Single().CanRunOnGraph;

        public string ToDescriptiveString(int depth)
        {
            var indent = new string(' ', depth * 4);
            var conditions = String.Join("", Conditions.Select(c => c.ToDescriptiveString(Unknown.Name, depth + 1)));
            return $"{indent}{Unknown.Name}: {Unknown.Type} [\n{conditions}{indent}]\n";
        }

        public override string ToString()
        {
            return ToDescriptiveString(0);
        }

        public Match Apply(ImmutableDictionary<string, string> replacements)
        {
            return new Match(Unknown, Conditions.Select(c => c.Apply(replacements)).ToImmutableList());
        }
    }
}