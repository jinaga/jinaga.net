using System;
using System.Collections.Immutable;
using Jinaga.Projections;
using Jinaga.Visualizers;

namespace Jinaga.Definitions
{
    public abstract class SymbolValue
    {
    }

    public class SymbolValueComposite : SymbolValue
    {
        public ImmutableDictionary<string, SymbolValue> Fields { get; }

        public SymbolValueComposite(ImmutableDictionary<string, SymbolValue> fields)
        {
            Fields = fields;
        }

        public SymbolValue GetField(string name)
        {
            if (Fields.TryGetValue(name, out var memberSet))
            {
                return memberSet;
            }
            else
            {
                var fieldNames = Fields.Keys.Join(", ");
                throw new ArgumentException($"Field not found in symbol table: {name} not in ({fieldNames})");
            }
        }

        public ProjectionDefinition CreateProjectionDefinition()
        {
            return new ProjectionDefinition(Fields);
        }
    }

    public class SymbolValueSetDefinition : SymbolValue
    {
        public SetDefinition SetDefinition { get; }

        public SymbolValueSetDefinition(SetDefinition setDefinition)
        {
            SetDefinition = setDefinition;
        }
    }

    public class SymbolValueCollection : SymbolValue
    {
        public SetDefinition StartSetDefinition { get; }
        public Specification Specification { get; }

        public SymbolValueCollection(SetDefinition startSetDefinition, Specification specification)
        {
            StartSetDefinition = startSetDefinition;
            Specification = specification;
        }
    }
}
