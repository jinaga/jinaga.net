using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Definitions
{
    public class SymbolTableOld 
    {
        public static SymbolTableOld Empty = new SymbolTableOld(ImmutableDictionary<string, SymbolValue>.Empty);
        private readonly ImmutableDictionary<string, SymbolValue> symbols;

        private SymbolTableOld(ImmutableDictionary<string, SymbolValue> symbols)
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
                throw new ArgumentException($"The symbol table {this} does not contain a member named {name}.");
            }
        }

        public SymbolTableOld With(string name, SymbolValue value)
        {
            return new SymbolTableOld(symbols.SetItem(name, value));
        }

        public override string ToString()
        {
            var symbolNames = string.Join(", ", symbols.Select(s => s.Key).ToArray());
            return $"({symbolNames})";
        }
    }
}
