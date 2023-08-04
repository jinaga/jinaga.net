namespace Jinaga.Store.SQLite.Description
{
    internal class OutputDescription
    {
        public string Label { get; }
        public string Type { get; }
        public int FactIndex { get; }

        public OutputDescription(string label, string type, int factIndex)
        {
            Label = label;
            Type = type;
            FactIndex = factIndex;
        }
    }
}