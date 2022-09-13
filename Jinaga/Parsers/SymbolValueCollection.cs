using Jinaga.Definitions;
using Jinaga.Projections;

namespace Jinaga.Parsers
{
    public class SymbolValueCollection : SymbolValue
    {
        public SetDefinition StartSetDefinition { get; }
        public Specification Specification { get; }

        public SymbolValueCollection(SetDefinition startSetDefinition, Specification specification)
        {
            StartSetDefinition = startSetDefinition;
            Specification = specification;
        }
    }
}