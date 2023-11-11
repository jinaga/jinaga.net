using Jinaga.Pipelines;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class SpecificationGiven
    {
        public Label Label { get; }
        public ImmutableList<ExistentialCondition> ExistentialConditions { get; }
        public bool CanRunOnGraph => !ExistentialConditions.Any();
        public SpecificationGiven(Label label, ImmutableList<ExistentialCondition> existentialConditions)
        {
            Label = label;
            ExistentialConditions = existentialConditions;
        }
    }
}