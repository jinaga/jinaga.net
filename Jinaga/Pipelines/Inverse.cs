using System.Collections.Immutable;
using Jinaga.Projections;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        public Pipeline InversePipeline { get; }
        public Subset InitialSubset { get; }
        public Operation Operation { get; }
        public Subset Subset { get; }
        public Projection Projection { get; }
        public ImmutableList<CollectionIdentifier> CollectionIdentifiers { get; }

        public Inverse(
            Pipeline inversePipeline,
            Subset initialSubset,
            Operation operation,
            Subset subset,
            Projection projection,
            ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            this.InversePipeline = inversePipeline;
            this.InitialSubset = initialSubset;
            this.Operation = operation;
            this.Subset = subset;
            this.Projection = projection;
            this.CollectionIdentifiers = collectionIdentifiers;
        }
    }
}