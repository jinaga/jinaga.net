using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class CompositeSetDefinition : SetDefinition
    {
        private readonly ImmutableDictionary<string, SimpleSetDefinition> fields;

        public CompositeSetDefinition(ImmutableDictionary<string, SimpleSetDefinition> fields)
        {
            this.fields = fields;
        }

        public SimpleSetDefinition GetField(string name)
        {
            if (fields.TryGetValue(name, out var memberSet))
            {
                return memberSet;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override SetDefinition WithSteps(string tag, StepsDefinition steps)
        {
            return new CompositeSetDefinition(fields.Select(field =>
                field.Key == tag
                    ? KeyValuePair.Create(tag, (SimpleSetDefinition)field.Value.WithSteps(tag, steps))
                    : field
            ).ToImmutableDictionary());
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
