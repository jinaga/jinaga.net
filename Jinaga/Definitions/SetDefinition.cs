using System;
using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class SetDefinition
    {
        private string factType;

        public SetDefinition(string factType)
        {
            this.factType = factType;
        }

        internal SetDefinition WithSteps(string parameterName, string parameterType, string startingTag, ImmutableList<Step> steps)
        {
            throw new NotImplementedException();
        }

        internal SetDefinition WithCondition(ConditionDefinition conditionDefinition)
        {
            throw new NotImplementedException();
        }

        internal SetDefinition Compose(SetDefinition continuation)
        {
            throw new NotImplementedException();
        }
    }
}
