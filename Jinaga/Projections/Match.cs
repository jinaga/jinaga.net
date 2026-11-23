using Jinaga.Pipelines;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class Match
    {
        public Match(Label unknown, ImmutableList<PathCondition> pathConditions, ImmutableList<ExistentialCondition> existentialConditions)
        {
            Unknown = unknown;
            PathConditions = pathConditions;
            ExistentialConditions = existentialConditions;
        }

        public Label Unknown { get; }
        public ImmutableList<PathCondition> PathConditions { get; }
        public ImmutableList<ExistentialCondition> ExistentialConditions { get; }

        public string ToDescriptiveString(int depth)
        {
            var indent = new string(' ', depth * 4);
            // Concat, not Union: a match may carry the same condition twice, and a set union
            // would print it once. Two conditions that read alike are two conditions, and a
            // specification that holds both describes itself as holding both.
            var conditions = String.Join("", PathConditions
                .Select(c => c.ToDescriptiveString(Unknown.Name, depth + 1))
            .Concat(ExistentialConditions
                .Select(c => c.ToDescriptiveString(Unknown.Name, depth + 1))));
            return $"{indent}{Unknown.Name}: {Unknown.Type} [\n{conditions}{indent}]\n";
        }

        public override string ToString()
        {
            return ToDescriptiveString(0);
        }

        public Match Apply(ImmutableDictionary<string, string> replacements)
        {
            Label unknown = Unknown;
            if (replacements.TryGetValue(unknown.Name, out var replacement))
            {
                unknown = new Label(replacement, unknown.Type);
            }
            return new Match(
                unknown,
                PathConditions.Select(c => c.Apply(replacements)).ToImmutableList(),
                ExistentialConditions.Select(c => c.Apply(replacements)).ToImmutableList()
            );
        }
    }
}