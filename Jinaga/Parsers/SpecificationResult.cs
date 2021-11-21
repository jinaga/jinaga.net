using System;
using System.Collections.Immutable;
using Jinaga.Definitions;
using Jinaga.Pipelines;

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

        public static SpecificationResult FromValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(symbolValue, ImmutableList<SpecificationVariable>.Empty);
        }

        public SpecificationResult WithValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(symbolValue, variables);
        }

        public SpecificationResult WithVariable(Label label, Type type)
        {
            return new SpecificationResult(
                SymbolValue,
                variables.Add(new SpecificationVariable(label, type))
            );
        }

        public SpecificationResult Compose(SpecificationResult other)
        {
            return new SpecificationResult(
                this.SymbolValue,
                this.variables.AddRange(other.variables)
            );
        }
    }
}
