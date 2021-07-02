namespace Jinaga.Facts
{
    public class FactReference
    {
        public FactReference(string type, string hash)
        {
            Type = type;
            Hash = hash;
        }

        public string Type { get; }
        public string Hash { get; }
    }
}