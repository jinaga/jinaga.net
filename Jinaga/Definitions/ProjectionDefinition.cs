using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Definitions
{
    public class ProjectionDefinition
    {
        private readonly ImmutableDictionary<string, SymbolValue> fields;

        public ProjectionDefinition(ImmutableDictionary<string, SymbolValue> fields)
        {
            this.fields = fields;
        }

        public IEnumerable<string> AllTags()
        {
            return fields
                .OrderBy(field => field.Key)
                .SelectMany(field => AllTags(field.Value).Prepend(field.Key));
        }

        private static IEnumerable<string> AllTags(SymbolValue value)
        {
            if (value is SymbolValueComposite compositeValue)
            {
                return compositeValue.Fields
                    .SelectMany(field => AllTags(field.Value).Prepend(field.Key));
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        public IEnumerable<SetDefinition> AllSetDefinitions()
        {
            return fields
                .OrderBy(field => field.Key)
                .SelectMany(field => AllSetDefinitions(field.Value));
        }

        private static IEnumerable<SetDefinition> AllSetDefinitions(SymbolValue value)
        {
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                return new [] { setDefinitionValue.SetDefinition };
            }
            else if (value is SymbolValueComposite compositValue)
            {
                return compositValue.Fields
                    .OrderBy(field => field.Key)
                    .SelectMany(field => AllSetDefinitions(field.Value));
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}