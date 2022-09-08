using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public abstract class MatchCondition
    {
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
    }
}