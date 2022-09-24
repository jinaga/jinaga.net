using System.Collections.Immutable;
using Jinaga.Projections;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        public Specification InverseSpecification { get; }
        public Subset InitialSubset { get; }
        public Operation Operation { get; }
        public Subset FinalSubset { get; }
        public ImmutableList<CollectionIdentifier> CollectionIdentifiers { get; }

        public Inverse(Specification inverseSpecification, Subset initialSubset, Operation operation, Subset finalSubset, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            InverseSpecification = inverseSpecification;
            InitialSubset = initialSubset;
            Operation = operation;
            FinalSubset = finalSubset;
            CollectionIdentifiers = collectionIdentifiers;
        }
    }
}