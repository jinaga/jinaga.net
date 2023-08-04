namespace Jinaga.Store.SQLite.Description
{
    internal class FactDescription
    {
        public FactDescription(string type, int factIndex)
        {
            Type = type;
            FactIndex = factIndex;
        }

        public string Type { get; }
        public int FactIndex { get; }
    }
}