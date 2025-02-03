using Jinaga.Projections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Specifications
{
    internal class ExistentialConditionContext : ConditionContext
    {
        public bool Exists { get; }
        public ImmutableList<Match> Matches { get; }
        public override IEnumerable<string> Labels => Matches.Select(m => m.Unknown.Name);
        
        public ExistentialConditionContext(bool exists, ImmutableList<Match> matches)
        {
            Exists = exists;
            Matches = matches;
        }
    }
}