using System;
using System.Collections.Immutable;

namespace Jinaga.Definitions
{
    public class SymbolValueComposite : SymbolValue
    {
        private readonly ImmutableDictionary<string, SetDefinition> fields;

        public SymbolValueComposite(ImmutableDictionary<string, SetDefinition> fields)
        {
            this.fields = fields;
        }

        public SetDefinition GetField(string name)
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

        public override SymbolValue WithSteps(string tag, StepsDefinition stepsDefinition)
        {
            throw new NotImplementedException();
        }

        public override SymbolValue WithCondition(ConditionDefinition conditionDefinition)
        {
            throw new NotImplementedException();
        }

        public override SymbolValue Compose(SymbolValue continuation, ProjectionDefinition projection)
        {
            throw new NotImplementedException();
        }
    }
}
