using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Definitions
{
    public class SymbolTable
    {
        public static SymbolTable Empty = new SymbolTable(ImmutableDictionary<string, SymbolValue>.Empty);
        private readonly ImmutableDictionary<string, SymbolValue> symbols;

        private SymbolTable(ImmutableDictionary<string, SymbolValue> symbols)
        {
            this.symbols = symbols;
        }

        public SymbolValue GetField(string name)
        {
            if (symbols.TryGetValue(name, out var memberSet))
            {
                return memberSet;
            }
            else
            {
                var symbolNames = string.Join(", ", symbols.Select(s => s.Key).ToArray());
                throw new ArgumentException($"The symbol table does not contain a member named {name}: {symbolNames}");
            }
        }

        public SymbolTable With(string name, SymbolValue value)
        {
            return new SymbolTable(this.symbols.Add(name, value));
        }
    }
}
