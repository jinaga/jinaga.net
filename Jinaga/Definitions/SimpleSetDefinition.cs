using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class SimpleSetDefinition : SetDefinition
    {
        private readonly string factType;
        private readonly StepsDefinition steps;
        private readonly ImmutableList<ConditionDefinition> conditions = ImmutableList<ConditionDefinition>.Empty;

        public SimpleSetDefinition(string factType)
        {
            this.factType = factType;
        }

        public SimpleSetDefinition(string factType, StepsDefinition steps, ImmutableList<ConditionDefinition> conditions)
        {
            this.factType = factType;
            this.steps = steps;
            this.conditions = conditions;
        }

        public override SetDefinition WithSteps(StepsDefinition steps)
        {
            return new SimpleSetDefinition(factType, steps, conditions);
        }

        public override SetDefinition WithCondition(ConditionDefinition condition)
        {
            return new SimpleSetDefinition(factType, steps, conditions.Add(condition));
        }

        public override SetDefinition Compose(SetDefinition continuation, ProjectionDefinition projection)
        {
            var fields = projection.Fields.ToImmutableDictionary(
                field => field.Name,
                field => field.Position == 0 ? this : continuation);
            return new CompositeSetDefinition(fields);
        }

        public override Pipeline CreatePipeline()
        {
            return steps.CreatePipeline(factType, conditions);
        }
    }
}
