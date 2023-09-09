using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Jinaga
{
    public class ShareTarget<T>
        where T : class
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

        public DistributionRules With(Expression<Func<T, User>> userSelector)
        {
            Specification userSpecification = Given<T>.Match(userSelector);
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, userSpecification)));
        }

        public DistributionRules With(Specification<T, User> userSpecification)
        {
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, userSpecification)));
        }
    }
}