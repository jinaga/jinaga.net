using System.Collections.Immutable;
using Jinaga.Projections;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        public Specification InverseSpecification { get; }

        public Inverse(Specification inverseSpecification)
        {
            InverseSpecification = inverseSpecification;
        }
    }
    public class InverseOld
    {
        public PipelineOld InversePipeline { get; }
        public Subset InitialSubset { get; }
        public Operation Operation { get; }
        public Subset FinalSubset { get; }
        public Projection Projection { get; }
        public ImmutableList<CollectionIdentifier> CollectionIdentifiers { get; }

        public InverseOld(
            PipelineOld inversePipeline,
            Subset initialSubset,
            Operation operation,
            Subset finalSubset,
            Projection projection,
            ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            this.InversePipeline = inversePipeline;
            this.InitialSubset = initialSubset;
            this.Operation = operation;
            this.FinalSubset = finalSubset;
            this.Projection = projection;
            this.CollectionIdentifiers = collectionIdentifiers;
        }
    }
}