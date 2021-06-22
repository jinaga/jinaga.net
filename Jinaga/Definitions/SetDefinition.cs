using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class SetDefinition
    {
        private readonly string factType;
        private readonly StepsDefinition steps;
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
            return new SetDefinition(factType, steps, conditions.Add(condition));
        }

        public string Tag => steps?.Tag;

        public Pipeline CreatePipeline()
        {
            return steps.CreatePipeline(factType, conditions);
        }
    }
}
