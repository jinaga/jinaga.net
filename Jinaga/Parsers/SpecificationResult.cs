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
        public ImmutableList<SetDefinition> SetDefinitions { get; }
        public ImmutableList<SetDefinitionTarget> Targets { get; }
        private ImmutableDictionary<SetDefinitionTarget, Label> labelByTarget;

        public SpecificationResult(
            SymbolValue symbolValue,
            ImmutableList<SpecificationVariable> variables,
            ImmutableHashSet<string> consumedTags,
            ImmutableList<SetDefinition> setDefinitions,
            ImmutableList<SetDefinitionTarget> targets,
            ImmutableDictionary<SetDefinitionTarget, Label> labelByTarget)
        {
            SymbolValue = symbolValue;
            this.variables = variables;
            this.consumedTags = consumedTags;
            SetDefinitions = setDefinitions;
            Targets = targets;
            this.labelByTarget = labelByTarget;
        }

        public static SpecificationResult FromValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(
                symbolValue,
                ImmutableList<SpecificationVariable>.Empty,
                ImmutableHashSet<string>.Empty,
                ImmutableList<SetDefinition>.Empty,
                ImmutableList<SetDefinitionTarget>.Empty,
                ImmutableDictionary<SetDefinitionTarget, Label>.Empty
            );
        }

        public SpecificationResult WithValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(
                symbolValue,
                variables,
                consumedTags,
                SetDefinitions,
                Targets,
                labelByTarget
            );
        }

        public SpecificationResult WithVariable(Label label, Type type)
        {
            return new SpecificationResult(
                SymbolValue,
                variables.Add(new SpecificationVariable(label, type)),
                consumedTags,
                SetDefinitions,
                Targets,
                labelByTarget
            );
        }

        public SpecificationResult ConsumeVariable(string consumedTag)
        {
            return new SpecificationResult(
                SymbolValue,
                variables,
                consumedTags.Add(consumedTag),
                SetDefinitions,
                Targets,
                labelByTarget
            );
        }

        public SpecificationResult WithSetDefinition(SetDefinition setDefinition)
        {
            return new SpecificationResult(
                SymbolValue,
                variables,
                consumedTags,
                SetDefinitions.Add(setDefinition),
                Targets,
                labelByTarget
            );
        }

        public SpecificationResult WithTarget(SetDefinitionTarget target)
        {
            return new SpecificationResult(
                SymbolValue,
                variables,
                consumedTags,
                SetDefinitions,
                Targets.Add(target),
                labelByTarget
            );
        }

        public SpecificationResult ApplyLabel(SetDefinitionTarget target, Label label)
        {
            return new SpecificationResult(
                SymbolValue,
                variables,
                consumedTags,
                SetDefinitions,
                Targets,
                labelByTarget.ContainsKey(target) ? labelByTarget : labelByTarget.Add(target, label)
            );
        }

        public SpecificationResult Compose(SpecificationResult other)
        {
            return new SpecificationResult(
                this.SymbolValue,
                this.variables.AddRange(other.variables),
                consumedTags.Union(other.consumedTags),
                SetDefinitions.AddRange(other.SetDefinitions),
                Targets.AddRange(other.Targets),
                labelByTarget.AddRange(other.labelByTarget)
            );
        }
    }
}
