using System;
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

        public override SetDefinition WithSteps(string tag, StepsDefinition steps)
        {
            return new SimpleSetDefinition(factType, steps, conditions);
        }

        public override SetDefinition WithCondition(ConditionDefinition condition)
        {
            return new SimpleSetDefinition(factType, steps, conditions.Add(condition));
        }

        public override SetDefinition Compose(SetDefinition continuation, ProjectionDefinition projection)
        {
            if (continuation is SimpleSetDefinition simpleSet)
            {
                var fields = projection.Fields.ToImmutableDictionary(
                    field => field.Name,
                    field => field.Position == 0 ? this : simpleSet);
                return new CompositeSetDefinition(fields);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public string Tag => steps?.Tag;

        public override Pipeline CreatePipeline()
        {
            return steps.CreatePipeline(factType, conditions);
        }
    }
}
