using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga
{
    public class ShareTarget
    {
        private readonly Specification specification;
        private readonly ImmutableList<DistributionRule> distributionRules;

        internal ShareTarget(Specification specification, ImmutableList<DistributionRule> distributionRules)
        {
            this.specification = specification;
            this.distributionRules = distributionRules;
        }

        public DistributionRules WithEveryone()
        {
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, null)));
        }
    }
}