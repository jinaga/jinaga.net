namespace Jinaga.Definitions
{
    public class SymbolValueSetDefinition : SymbolValue
    {
        public SetDefinition SetDefinition { get; }

        public SymbolValueSetDefinition(SetDefinition setDefinition)
        {
            SetDefinition = setDefinition;
        }
    }
}
