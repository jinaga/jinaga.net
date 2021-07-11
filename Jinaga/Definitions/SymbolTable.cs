using System;
using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class SymbolTable
    {
        private readonly ImmutableDictionary<string, SymbolValue> symbols;

        public static SymbolTable WithParameter(string name, string type)
        {
            var startingSet = new SetDefinitionInitial(type, name);
            var symbolTable = new SymbolTable(ImmutableDictionary<string, SymbolValue>.Empty.Add(name, new SymbolValueSetDefinition(startingSet)));
            return symbolTable;
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
                throw new NotImplementedException();
            }
        }

        public SymbolTable With(string name, SymbolValue value)
        {
            return new SymbolTable(this.symbols.Add(name, value));
        }
    }
}
