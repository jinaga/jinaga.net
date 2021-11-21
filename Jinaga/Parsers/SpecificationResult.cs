using System.Collections.Immutable;
using Jinaga.Definitions;

namespace Jinaga.Parsers
{
    public class SpecificationResult
    {
        public SymbolValue SymbolValue { get; }
        private ImmutableList<SpecificationVariable> variables;

        private SpecificationResult(SymbolValue symbolValue, ImmutableList<SpecificationVariable> variables)
        {
            SymbolValue = symbolValue;
            this.variables = variables;
        }

        public static SpecificationResult WithValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(symbolValue, ImmutableList<SpecificationVariable>.Empty);
        }
    }
}
