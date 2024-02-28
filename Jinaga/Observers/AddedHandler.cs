using Jinaga.Facts;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public FactReferenceTuple Anchor { get; }
        public string Path { get; }
        public OnAddedHandler Added { get; }

        public AddedHandler(FactReferenceTuple anchor, string path, OnAddedHandler added)
        {
            Anchor = anchor;
            Path = path;
            Added = added;
        }
    }
}