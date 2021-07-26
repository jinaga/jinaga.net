using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Definitions
{
    public class SymbolTable
    {
        private readonly ImmutableDictionary<string, SymbolValue> symbols;

        public static SymbolTable WithParameter(string name, string type)
        {
            var startingSet = new SetDefinitionInitial(name, type);
            var symbolTable = new SymbolTable(ImmutableDictionary<string, SymbolValue>.Empty.Add(name, new SymbolValueSetDefinition(startingSet)));
            return symbolTable;
        }

        public static SymbolTable WithSymbol(string name, SymbolValue value)
        {
            return new SymbolTable(ImmutableDictionary<string, SymbolValue>.Empty.Add(name, value));
        }

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
