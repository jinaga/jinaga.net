using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public abstract class MatchCondition
    {
        public abstract string ToDescriptiveString(string unknown, int v);
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

        public override string ToDescriptiveString(string unknown, int depth)
        {
            var indent = new string(' ', depth * 4);
            var rolesLeft = String.Join("", RolesLeft.Select(r => $"->{r.Name}: {r.TargetType}"));
            var rolesRight = String.Join("", RolesRight.Select(r => $"->{r.Name}: {r.TargetType}"));
            return $"{indent}{unknown}{rolesLeft} = {LabelRight}{rolesRight}\n";
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

        public override string ToDescriptiveString(string unknown, int depth)
        {
            throw new NotImplementedException();
        }
    }
}