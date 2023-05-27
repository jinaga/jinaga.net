using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    internal class ExistentialConditionContext : ConditionContext
    {
        public bool Exists { get; }
        public ImmutableList<Match> Matches { get; }
        
        public ExistentialConditionContext(bool exists, ImmutableList<Match> matches)
        {
            Exists = exists;
            Matches = matches;
        }
    }
}