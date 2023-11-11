using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class ExistentialCondition
    {
        public ExistentialCondition(bool exists, ImmutableList<Match> matches)
        {
            Exists = exists;
            Matches = matches;
        }

        public bool Exists { get; }
        public ImmutableList<Match> Matches { get; }

        public bool CanRunOnGraph => Matches.All(m => m.CanRunOnGraph);

        public string ToDescriptiveString(string unknown, int depth)
        {
            var indent = new string(' ', depth * 4);
            var matches = String.Join("", Matches.Select(m => m.ToDescriptiveString(depth + 1)));
            var op = Exists ? "" : "!";
            return $"{indent}{op}E {{\n{matches}{indent}}}\n";
        }

        public ExistentialCondition Apply(ImmutableDictionary<string, string> replacements)
        {
            return new ExistentialCondition(Exists, Matches.Select(m => m.Apply(replacements)).ToImmutableList());
        }
    }
}