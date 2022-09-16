using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public abstract class MatchCondition
    {
        public abstract bool CanRunOnGraph { get; }

        public abstract string ToDescriptiveString(string unknown, int v);
        public abstract MatchCondition Apply(ImmutableDictionary<string, string> replacements);
    }

    public class PathCondition : MatchCondition
    {
        public PathCondition(ImmutableList<Role> rolesLeft, string labelRight, ImmutableList<Role> rolesRight)
        {
            RolesLeft = rolesLeft;
            LabelRight = labelRight;
            RolesRight = rolesRight;
        }

        public ImmutableList<Role> RolesLeft { get; }
        public string LabelRight { get; }
        public ImmutableList<Role> RolesRight { get; }

        public override bool CanRunOnGraph => RolesLeft.Count == 0;

        public override string ToDescriptiveString(string unknown, int depth)
        {
            var indent = new string(' ', depth * 4);
            var rolesLeft = String.Join("", RolesLeft.Select(r => $"->{r.Name}: {r.TargetType}"));
            var rolesRight = String.Join("", RolesRight.Select(r => $"->{r.Name}: {r.TargetType}"));
            return $"{indent}{unknown}{rolesLeft} = {LabelRight}{rolesRight}\n";
        }

        public override MatchCondition Apply(ImmutableDictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(LabelRight, out var replacement))
            {
                return new PathCondition(RolesLeft, replacement, RolesRight);
            }
            else
            {
                return this;
            }
        }
    }

    public class ExistentialCondition : MatchCondition
    {
        public ExistentialCondition(bool exists, ImmutableList<Match> matches)
        {
            Exists = exists;
            Matches = matches;
        }

        public bool Exists { get; }
        public ImmutableList<Match> Matches { get; }

        public override bool CanRunOnGraph => Matches.All(m => m.CanRunOnGraph);

        public override string ToDescriptiveString(string unknown, int depth)
        {
            var indent = new string(' ', depth * 4);
            var matches = String.Join("", Matches.Select(m => m.ToDescriptiveString(depth + 1)));
            var op = Exists ? "" : "!";
            return $"{indent}{op}E {{\n{matches}{indent}}}\n";
        }

        public override MatchCondition Apply(ImmutableDictionary<string, string> replacements)
        {
            return new ExistentialCondition(Exists, Matches.Select(m => m.Apply(replacements)).ToImmutableList());
        }
    }
}