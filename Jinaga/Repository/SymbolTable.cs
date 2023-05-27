using Jinaga.Projections;
using System;
using System.Collections.Immutable;

namespace Jinaga.Repository
{
    internal class SymbolTable
    {
        private readonly ImmutableDictionary<string, Projection> values;

        private SymbolTable(ImmutableDictionary<string, Projection> values)
        {
            this.values = values;
        }

        public static SymbolTable Empty = new SymbolTable(ImmutableDictionary<string, Projection>.Empty);

        internal SymbolTable Set(string name, Projection value)
        {
            return new SymbolTable(values.SetItem(name, value));
        }

        internal Projection Get(string name)
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