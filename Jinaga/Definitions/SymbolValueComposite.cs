using System;
using System.Collections.Immutable;

namespace Jinaga.Definitions
{
    public class SymbolValueComposite : SymbolValue
    {
        private readonly ImmutableDictionary<string, SymbolValue> fields;

        public SymbolValueComposite(ImmutableDictionary<string, SymbolValue> fields)
        {
            this.fields = fields;
        }

        public SymbolValue GetField(string name)
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

        public override SymbolValue WithSteps(StepsDefinition stepsDefinition)
        {
            var tag = stepsDefinition.Tag;
            return new SymbolValueComposite(fields
                .ToImmutableDictionary(
                    field => field.Key,
                    field => field.Key == tag
                        ? field.Value.WithSteps(stepsDefinition)
                        : field.Value
                )
            );
        }

        public override SymbolValue WithCondition(ConditionDefinition conditionDefinition)
        {
            var tag = conditionDefinition.InitialFactName;
            return new SymbolValueComposite(fields
                .ToImmutableDictionary(
                    field => field.Key,
                    field => field.Key == tag
                        ? field.Value.WithCondition(conditionDefinition)
                        : field.Value
                )
            );
        }
    }
}
