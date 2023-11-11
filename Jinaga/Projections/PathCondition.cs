using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class PathCondition
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

        public bool CanRunOnGraph => RolesLeft.Count == 0;

        public string ToDescriptiveString(string unknown, int depth)
        {
            var indent = new string(' ', depth * 4);
            var rolesLeft = String.Join("", RolesLeft.Select(r => $"->{r.Name}: {r.TargetType}"));
            var rolesRight = String.Join("", RolesRight.Select(r => $"->{r.Name}: {r.TargetType}"));
            return $"{indent}{unknown}{rolesLeft} = {LabelRight}{rolesRight}\n";
        }

        public PathCondition Apply(ImmutableDictionary<string, string> replacements)
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
}