using System;
using System.Collections.Immutable;

namespace Jinaga.Repository
{
    internal class SymbolTable
    {
        private readonly ImmutableDictionary<string, Value> values;

        private SymbolTable(ImmutableDictionary<string, Value> values)
        {
            this.values = values;
        }

        public static SymbolTable Empty = new SymbolTable(ImmutableDictionary<string, Value>.Empty);

        internal SymbolTable Set(string name, Value value)
        {
            return new SymbolTable(values.SetItem(name, value));
        }

        internal Value Get(string name)
        {
            if (values.TryGetValue(name, out var value))
            {
                return value;
            }
            else
            {
                throw new Exception($"No value named {name}.");
            }
        }
    }
}