using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Jinaga.Definitions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class SpecificationResult
    {
        public SymbolValue SymbolValue { get; }
        public ImmutableList<SetDefinition> SetDefinitions { get; }
        public ImmutableList<SetDefinitionTarget> Targets { get; }
        private ImmutableDictionary<SetDefinitionTarget, Label> labelByTarget;

        public SpecificationResult(
            SymbolValue symbolValue,
            ImmutableList<SetDefinition> setDefinitions,
            ImmutableList<SetDefinitionTarget> targets,
            ImmutableDictionary<SetDefinitionTarget, Label> labelByTarget)
        {
            SymbolValue = symbolValue;
            SetDefinitions = setDefinitions;
            Targets = targets;
            this.labelByTarget = labelByTarget;
        }

        public static SpecificationResult FromValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(
                symbolValue,
                ImmutableList<SetDefinition>.Empty,
                ImmutableList<SetDefinitionTarget>.Empty,
                ImmutableDictionary<SetDefinitionTarget, Label>.Empty
            );
        }

        public SpecificationResult WithValue(SymbolValue symbolValue)
        {
            return new SpecificationResult(
                symbolValue,
                SetDefinitions,
                Targets,
                labelByTarget
            );
        }

        public SpecificationResult WithSetDefinition(SetDefinition setDefinition)
        {
            return new SpecificationResult(
                SymbolValue,
                SetDefinitions.Add(setDefinition),
                Targets,
                labelByTarget
            );
        }

        public SpecificationResult WithTarget(SetDefinitionTarget target)
        {
            return new SpecificationResult(
                SymbolValue,
                SetDefinitions,
                Targets.Add(target),
                labelByTarget
            );
        }

        public SpecificationResult ApplyLabel(SetDefinitionTarget target, Label label)
        {
            return new SpecificationResult(
                SymbolValue,
                SetDefinitions,
                Targets,
                labelByTarget.ContainsKey(target) ? labelByTarget : labelByTarget.Add(target, label)
            );
        }

        public SpecificationResult Compose(SpecificationResult other)
        {
            return new SpecificationResult(
                this.SymbolValue,
                SetDefinitions.AddRange(other.SetDefinitions),
                Targets.AddRange(other.Targets),
                labelByTarget.AddRange(other.labelByTarget)
            );
        }

        public bool TryGetLabelOf(SetDefinitionTarget target, [MaybeNullWhen(false)] out Label label)
        {
            return labelByTarget.TryGetValue(target, out label);
        }

        public Label GetLabelOf(SetDefinitionTarget target)
        {
            return labelByTarget[target];
        }
    }
}
