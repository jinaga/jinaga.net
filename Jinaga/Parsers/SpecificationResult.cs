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
        private ImmutableHashSet<string> consumedTags;

        public SpecificationResult(SymbolValue symbolValue, ImmutableList<SpecificationVariable> variables, ImmutableHashSet<string> consumedTags)
        {
            SymbolValue = symbolValue;
            this.variables = variables;
            this.consumedTags = consumedTags;
        }

        public static SpecificationResult FromValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(
                symbolValue,
                ImmutableList<SpecificationVariable>.Empty,
                ImmutableHashSet<string>.Empty
            );
        }

        public SpecificationResult WithValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(symbolValue, variables, consumedTags);
        }

        public SpecificationResult WithVariable(Label label, Type type)
        {
            return new SpecificationResult(
                SymbolValue,
                variables.Add(new SpecificationVariable(label, type)),
                consumedTags
            );
        }

        public SpecificationResult ConsumeVariable(string consumedTag)
        {
            return new SpecificationResult(
                SymbolValue,
                variables,
                consumedTags.Add(consumedTag)
            );
        }

        public SpecificationResult Compose(SpecificationResult other)
        {
            return new SpecificationResult(
                this.SymbolValue,
                this.variables.AddRange(other.variables),
                consumedTags.Union(other.consumedTags)
            );
        }
    }
}
