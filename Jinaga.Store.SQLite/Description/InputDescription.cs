namespace Jinaga.Store.SQLite.Description
{
    internal class InputDescription
    {
        public string Label { get; }
        public string Type { get; }
        public int FactIndex { get; }
        public int FactTypeParameter { get; }
        public int FactHashParameter { get; }

        public InputDescription(string label, string type, int factIndex, int factTypeParameter, int factHashParameter)
        {
            Label = label;
            Type = type;
            FactIndex = factIndex;
            FactTypeParameter = factTypeParameter;
            FactHashParameter = factHashParameter;
        }
    }
}