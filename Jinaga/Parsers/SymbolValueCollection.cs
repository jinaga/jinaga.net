using Jinaga.Definitions;
using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Parsers
{
    public class SymbolValueCollection : SymbolValue
    {
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        public SymbolValueCollection(ImmutableList<Match> matches, Projection projection)
        {
            Matches = matches;
            Projection = projection;
        }
    }
}