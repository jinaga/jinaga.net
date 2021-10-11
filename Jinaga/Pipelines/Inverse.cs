using System.Collections.Immutable;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        private readonly Pipeline inversePipeline;
        private readonly string affectedTag;
        private readonly Operation operation;
        private readonly Subset subset;
        private readonly ImmutableList<CollectionIdentifier> collectionIdentifiers;

        public Inverse(Pipeline inversePipeline, string affectedTag, Operation operation, Subset subset, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            this.inversePipeline = inversePipeline;
            this.affectedTag = affectedTag;
            this.operation = operation;
            this.subset = subset;
            this.collectionIdentifiers = collectionIdentifiers;
        }

        public Pipeline InversePipeline => inversePipeline;

        public string AffectedTag => affectedTag;

        public Operation Operation => operation;
        public Subset Subset => subset;

        public ImmutableList<CollectionIdentifier> CollectionIdentifiers => collectionIdentifiers;
    }
}