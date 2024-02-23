using Jinaga.Projections;

namespace Jinaga.Pipelines
{
    public class Inverse
    {
        public Specification InverseSpecification { get; }
        public Subset GivenSubset { get; }
        public InverseOperation Operation { get; }
        public Subset ResultSubset { get; }
        public string Path { get; }
        public Subset ParentSubset { get; }

        public Inverse(Specification inverseSpecification, Subset givenSubset, InverseOperation operation, Subset resultSubset, string path, Subset parentSubset)
        {
            InverseSpecification = inverseSpecification;
            GivenSubset = givenSubset;
            Operation = operation;
            ResultSubset = resultSubset;
            Path = path;
            ParentSubset = parentSubset;
        }
    }
}