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

        public ProjectionDefinition CreateProjectionDefinition()
        {
            return new ProjectionDefinition(fields);
        }
    }
}
