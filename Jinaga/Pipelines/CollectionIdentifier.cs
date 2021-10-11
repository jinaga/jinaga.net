namespace Jinaga.Pipelines
{
    public class CollectionIdentifier
    {
        public CollectionIdentifier(string collectionName)
        {
            CollectionName = collectionName;
        }

        public string CollectionName { get; }
    }
}