using System.Collections.Immutable;
using Jinaga.Projections;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        public Specification InverseSpecification { get; }
        public Subset GivenSubset { get; }
        public InverseOperation Operation { get; }
        public Subset FinalSubset { get; }
        public ImmutableList<CollectionIdentifier> CollectionIdentifiers { get; }

        public Inverse(Specification inverseSpecification, Subset givenSubset, InverseOperation operation, Subset finalSubset, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            InverseSpecification = inverseSpecification;
            GivenSubset = givenSubset;
            Operation = operation;
            FinalSubset = finalSubset;
            CollectionIdentifiers = collectionIdentifiers;
        }
    }
}