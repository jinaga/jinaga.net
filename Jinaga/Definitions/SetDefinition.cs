using System;
using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class SetDefinition
    {
        private readonly string factType;
        private readonly StepsDefinition? steps;
        private readonly ImmutableList<ConditionDefinition> conditions = ImmutableList<ConditionDefinition>.Empty;

        public SetDefinition(string factType)
        {
            this.factType = factType;
        }

        public SetDefinition(string factType, StepsDefinition steps, ImmutableList<ConditionDefinition> conditions)
        {
            this.factType = factType;
            this.steps = steps;
            this.conditions = conditions;
        }

        public SetDefinition WithSteps(StepsDefinition steps)
        {
            return new SetDefinition(factType, steps, conditions);
        }

        public SetDefinition WithCondition(ConditionDefinition condition)
        {
            if (steps == null)
            {
                throw new InvalidOperationException("Using an uninitialized set definition");
            }
            return new SetDefinition(factType, steps, conditions.Add(condition));
        }

        public bool IsInitialized => steps != null;
        public string Tag => steps == null
            ? throw new InvalidOperationException("Using an uninitialized set definition")
            : steps.Tag;

        public string InitialFactName => steps == null
            ? throw new InvalidOperationException("Using an uninitialized set definition")
            : steps.InitialFactName;

        public Pipeline CreatePipeline()
        {
            if (steps == null)
            {
                throw new InvalidOperationException("Using an uninitialized set definition");
            }
            
            return steps.CreatePipeline(factType, conditions);
        }
    }
}
