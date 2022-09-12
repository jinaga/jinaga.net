using Jinaga.Definitions;
using Jinaga.Projections;

namespace Jinaga.Parsers
{
    internal class SymbolValueCollection : SymbolValue
    {
        private SetDefinition startSetDefinition;
        private Specification specification;

        public SymbolValueCollection(SetDefinition startSetDefinition, Specification specification)
        {
            this.startSetDefinition = startSetDefinition;
            this.specification = specification;
        }
    }
}