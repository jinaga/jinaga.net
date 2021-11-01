namespace Jinaga.Pipelines
{
    public class CollectionIdentifier
    {
        public CollectionIdentifier(string collectionName, Subset intermediateSubset)
        {
            CollectionName = collectionName;
            IntermediateSubset = intermediateSubset;
        }

        public string CollectionName { get; }
        public Subset IntermediateSubset { get; }
    }
}