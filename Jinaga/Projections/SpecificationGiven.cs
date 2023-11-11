using Jinaga.Pipelines;
using System.Collections.Immutable;

namespace Jinaga.Projections
{
    public class SpecificationGiven
    {
        public Label Label { get; }
        public ImmutableList<ExistentialCondition> ExistentialConditions { get; }

        public SpecificationGiven(Label label, ImmutableList<ExistentialCondition> existentialConditions)
        {
            Label = label;
            ExistentialConditions = existentialConditions;
        }
    }
}