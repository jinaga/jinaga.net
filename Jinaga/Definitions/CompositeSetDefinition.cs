using System.Collections.Immutable;
using System;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class CompositeSetDefinition : SetDefinition
    {
        private readonly ImmutableDictionary<string, SetDefinition> fields;

        public CompositeSetDefinition(ImmutableDictionary<string, SetDefinition> fields)
        {
            this.fields = fields;
        }

        public override SetDefinition WithSteps(StepsDefinition steps)
        {
            throw new NotImplementedException();
        }

        public override SetDefinition WithCondition(ConditionDefinition condition)
        {
            throw new NotImplementedException();
        }

        public override SetDefinition Compose(SetDefinition continuation, ProjectionDefinition projection)
        {
            throw new NotImplementedException();
        }

        public override Pipeline CreatePipeline()
        {
            throw new NotImplementedException();
        }
    }
}
